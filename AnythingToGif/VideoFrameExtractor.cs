using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

public static class VideoFrameExtractor {

  private unsafe struct FFmpegState : IDisposable {

    private const int STATUS_OK = 0;

    private readonly List<Action> _disposals = new();

    private readonly AVFormatContext*[] _pFormatContext = new AVFormatContext*[1];
    private readonly AVCodecContext*[] _pCodecContext = new AVCodecContext*[1];
    private readonly AVFrame*[] _pFrame = new AVFrame*[1];
    private readonly AVFrame*[] _pRgbFrame = new AVFrame*[1];
    private readonly AVPacket*[] _pPacket = new AVPacket*[1];
    private readonly AVCodecParserContext*[] _pParser = new AVCodecParserContext*[1];
    private readonly SwsContext*[] _pSwsContext = new SwsContext*[1];
    private AVStream* _pStream;
    private IntPtr _convertedFrameBufferPtr;
    private byte_ptrArray4 _dstData;
    private int_array4 _dstLinesize;

    public FFmpegState(FileInfo video) {
      var self = this;
      ffmpeg.avdevice_register_all();

      // allocate memory for context
      this._pFormatContext[0] = ffmpeg.avformat_alloc_context();
      if (this._pFormatContext[0] == null)
        throw new ApplicationException("Could not allocate format context.");
      
      this._disposals.Add(() => {
        ffmpeg.avformat_free_context(self._pFormatContext[0]);
        self._pFormatContext[0] = null;
      });

      // open format
      fixed (AVFormatContext** pFormatContextPtr = &this._pFormatContext[0])
        if (ffmpeg.avformat_open_input(pFormatContextPtr, video.FullName, null, null) != STATUS_OK)
          throw new ApplicationException("Could not open file.");

      this._disposals.Add(()=> {
        fixed (AVFormatContext** pFormatContextPtr = &self._pFormatContext[0])
          ffmpeg.avformat_close_input(pFormatContextPtr);

        self._pFormatContext[0] = null;
      });

      // is there a stream in it?
      if (ffmpeg.avformat_find_stream_info(this._pFormatContext[0], null) != STATUS_OK) {
        this.Dispose();
        throw new ApplicationException("Could not find stream info.");
      }

      // find the video stream
      for (var i = 0; i < this._pFormatContext[0]->nb_streams; ++i) {
        var avStream = this._pFormatContext[0]->streams[i];
        if (avStream->codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_VIDEO)
          continue;

        this._pStream = avStream;
        break;
      }

      if (this._pStream == null) {
        this.Dispose();
        throw new ApplicationException("Could not find video stream.");
      }

      this._disposals.Add(() => self._pStream = null);

      // calculate durations
      this._frameDuration = new(
        TimeSpan.FromSeconds(ffmpeg.av_q2d(this._pStream->time_base)),
        TimeSpan.FromSeconds(1 / ffmpeg.av_q2d(this._pStream->r_frame_rate)),
        TimeSpan.FromSeconds(1 / ffmpeg.av_q2d(this._pStream->avg_frame_rate))
      );

      // find codec for the selected stream
      var pCodec = ffmpeg.avcodec_find_decoder(this._pStream->codecpar->codec_id);
      if (pCodec == null) {
        this.Dispose();
        throw new ApplicationException("Unsupported codec.");
      }

      // get memory to store a codec context
      this._pCodecContext[0] = ffmpeg.avcodec_alloc_context3(pCodec);
      if (this._pCodecContext[0] == null) {
        this.Dispose();
        throw new ApplicationException("Could not allocate codec context.");
      }

      this._disposals.Add(() => {
        fixed(AVCodecContext** pCodecContextPtr = &self._pCodecContext[0])
          ffmpeg.avcodec_free_context(pCodecContextPtr);

        self._pCodecContext[0] = null;
      });

      // copy codec parameters to context
      if (ffmpeg.avcodec_parameters_to_context(this._pCodecContext[0], this._pStream->codecpar) < STATUS_OK) {
        this.Dispose();
        throw new ApplicationException("Could not copy codec parameters to context.");
      }

      // open codec
      if (ffmpeg.avcodec_open2(this._pCodecContext[0], pCodec, null) < STATUS_OK) {
        this.Dispose();
        throw new ApplicationException("Could not open codec.");
      }

      // allocate memory for frame
      this._pFrame[0] = ffmpeg.av_frame_alloc();
      if (this._pFrame[0] == null) {
        this.Dispose();
        throw new ApplicationException("Could not allocate video frame.");
      }

      this._disposals.Add(()=> {
        fixed (AVFrame** pFramePtr = &self._pFrame[0])
          ffmpeg.av_frame_free(pFramePtr);
        
        self._pFrame[0] = null;
      });

      // allocate memory for rgb frame
      this._pRgbFrame[0] = ffmpeg.av_frame_alloc();
      if (this._pRgbFrame[0] == null) {
        this.Dispose();
        throw new ApplicationException("Could not allocate RGB frame.");
      }

      this._disposals.Add(() => {
        fixed (AVFrame** pRgbFramePtr = &self._pRgbFrame[0])
          ffmpeg.av_frame_free(pRgbFramePtr);

        self._pRgbFrame[0] = null;
      });

      // allocate memory for packet
      this._pPacket[0] = ffmpeg.av_packet_alloc();
      if (this._pPacket[0] == null) {
        this.Dispose();
        throw new ApplicationException("Could not allocate packet.");
      }

      this._disposals.Add(() => {
        fixed (AVPacket** pPacketPtr = &self._pPacket[0])
          ffmpeg.av_packet_free(pPacketPtr);

        self._pPacket[0] = null;
      });

      // init parser
      this._pParser[0] = ffmpeg.av_parser_init((int)pCodec->id);
      if (this._pParser[0] == null) {
        this.Dispose();
        throw new ApplicationException("Could not initialize parser.");
      }

      this._disposals.Add(() => {
        fixed (AVCodecParserContext** pParserPtr = &self._pParser[0])
          ffmpeg.av_parser_close(*pParserPtr);

        self._pParser[0] = null;
      });

      // init video2rgb converter
      this._pSwsContext[0] = ffmpeg.sws_getContext(
        this._pCodecContext[0]->width,
        this._pCodecContext[0]->height,
        this._pCodecContext[0]->pix_fmt,
        this._pCodecContext[0]->width,
        this._pCodecContext[0]->height,
        AVPixelFormat.AV_PIX_FMT_BGR24,
        ffmpeg.SWS_BILINEAR,
        null,
        null,
        null
      );

      if (this._pSwsContext[0] == null) {
        this.Dispose();
        throw new ApplicationException("Could not initialize the conversion context.");
      }

      this._disposals.Add(() => {
        ffmpeg.sws_freeContext(self._pSwsContext[0]);
        self._pSwsContext[0] = null;
      });

      var numBytes = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, this._pCodecContext[0]->width, this._pCodecContext[0]->height, 1);
      this._convertedFrameBufferPtr = Marshal.AllocHGlobal(numBytes);
      this._disposals.Add(() => {
        Marshal.FreeHGlobal(self._convertedFrameBufferPtr);
        self._convertedFrameBufferPtr = IntPtr.Zero;
      });

      this._dstData = new();
      this._dstLinesize = new();

      ffmpeg.av_image_fill_arrays(ref this._dstData, ref this._dstLinesize, (byte*)this._convertedFrameBufferPtr, AVPixelFormat.AV_PIX_FMT_BGR24, this._pCodecContext[0]->width, this._pCodecContext[0]->height, 1);
    }

    private readonly record struct FrameDuration(TimeSpan timePerPts, TimeSpan minimumFrameTime, TimeSpan averageFrameTime);

    private long _previousPts = 0;
    private readonly FrameDuration _frameDuration;
    
    public bool TryGetNextFrame([NotNullWhen(true)] out Bitmap? result, out TimeSpan duration) {
      result = null;
      duration = TimeSpan.Zero;

      var ptsFactor = ffmpeg.av_q2d(this._pStream->time_base);
      fixed (AVPacket** packetPtr = &this._pPacket[0])
        while (ffmpeg.av_read_frame(this._pFormatContext[0], *packetPtr) >= FFmpegState.STATUS_OK) {

          // was it a frame for our video stream?
          if (this._pPacket[0]->stream_index != this._pStream->index) {
            ffmpeg.av_packet_unref(this._pPacket[0]);
            continue;
          }

          // can we send the frame to our decoder?
          if (ffmpeg.avcodec_send_packet(this._pCodecContext[0], this._pPacket[0]) < FFmpegState.STATUS_OK) {
            ffmpeg.av_packet_unref(this._pPacket[0]);
            continue;
          }

          // can he decode it?
          if (ffmpeg.avcodec_receive_frame(this._pCodecContext[0], this._pFrame[0]) != FFmpegState.STATUS_OK) {
            ffmpeg.av_packet_unref(this._pPacket[0]);
            continue;
          }

          // convert to RGB
          ffmpeg.sws_scale(
            this._pSwsContext[0],
            this._pFrame[0]->data,
            this._pFrame[0]->linesize,
            0,
            this._pCodecContext[0]->height,
            this._dstData,
            this._dstLinesize
          );

          this._pRgbFrame[0]->data.UpdateFrom(this._dstData);
          this._pRgbFrame[0]->linesize.UpdateFrom(this._dstLinesize);

          var width = this._pCodecContext[0]->width;
          var height = this._pCodecContext[0]->height;

          result = ConvertToBitmap(width, height, this._dstData[0], this._dstLinesize[0]);

          var pts = this._pPacket[0]->pts == ffmpeg.AV_NOPTS_VALUE ? this._previousPts + 1 : this._pPacket[0]->pts;
          var ptsDelta = pts - this._previousPts;
          this._previousPts = pts;

          var ptsDuration = this._frameDuration.minimumFrameTime.MultipliedWith((this._frameDuration.timePerPts.MultipliedWith(ptsDelta)/this._frameDuration.minimumFrameTime).Round());
          duration = this._frameDuration.averageFrameTime;
          
          ffmpeg.av_packet_unref(this._pPacket[0]);
          return true;
        }

      return false;

      Bitmap ConvertToBitmap(int width, int height, byte* srcFrame, int srcStride) {
        var result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        BitmapData? bitmapData = null;
        try {
          bitmapData = result.LockBits(new(0, 0, width, height), ImageLockMode.WriteOnly, result.PixelFormat);

          var dst = (byte*)bitmapData.Scan0;
          var dstStride = bitmapData.Stride;

          if (srcStride == dstStride)
            Buffer.MemoryCopy(srcFrame, dst, dstStride * height, srcStride * height);
          else
            for (var y = 0; y < height; ++y) {
              Buffer.MemoryCopy(srcFrame, dst, dstStride, srcStride);
              srcFrame += srcStride;
              dst += dstStride;
            }
        } finally {
          if (bitmapData != null)
            result.UnlockBits(bitmapData);
        }

        return result;
      }

    }

    public void Dispose() {
      for (var i = this._disposals.Count - 1; i >= 0; --i) {
        this._disposals[i]();
        this._disposals.RemoveAt(i);
      }
    }

  }

  public static IEnumerable<(Bitmap frame, TimeSpan duration)> GetFrames(FileInfo video) {
    using var state = new FFmpegState(video);
    while (state.TryGetNextFrame(out var frame, out var duration))
      yield return (frame, duration);
  }

}
