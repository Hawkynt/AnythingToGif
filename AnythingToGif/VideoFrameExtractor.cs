using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

public static class VideoFrameExtractor {
  private unsafe struct FFmpegState : IDisposable {
    private readonly AVFormatContext*[] pFormatContext = new AVFormatContext*[1];
    private readonly AVCodecContext*[] pCodecContext = new AVCodecContext*[1];
    private readonly AVFrame*[] pFrame = new AVFrame*[1];
    private readonly AVFrame*[] pRgbFrame = new AVFrame*[1];
    private readonly AVPacket*[] pPacket = new AVPacket*[1];
    private readonly AVCodecParserContext*[] pParser = new AVCodecParserContext*[1];
    private readonly SwsContext*[] pSwsContext = new SwsContext*[1];
    private readonly AVStream* pStream;
    private long previousPts = 0; 
    private readonly IntPtr convertedFrameBufferPtr;
    private byte_ptrArray4 dstData;
    private int_array4 dstLinesize;

    public FFmpegState(FileInfo video) {
      ffmpeg.avdevice_register_all();

      pFormatContext[0] = ffmpeg.avformat_alloc_context();
      if (pFormatContext[0] == null)
        throw new ApplicationException("Could not allocate format context.");

      fixed (AVFormatContext** pFormatContextPtr = &pFormatContext[0]) {
        if (ffmpeg.avformat_open_input(pFormatContextPtr, video.FullName, null, null) != 0)
          throw new ApplicationException("Could not open file.");
      }

      if (ffmpeg.avformat_find_stream_info(pFormatContext[0], null) != 0) {
        Dispose();
        throw new ApplicationException("Could not find stream info.");
      }

      for (var i = 0; i < pFormatContext[0]->nb_streams; i++) {
        var avStream = pFormatContext[0]->streams[i];
        if (avStream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO) {
          pStream = avStream;
          break;
        }
      }

      if (pStream == null) {
        Dispose();
        throw new ApplicationException("Could not find video stream.");
      }

      var pCodec = ffmpeg.avcodec_find_decoder(pStream->codecpar->codec_id);
      if (pCodec == null) {
        Dispose();
        throw new ApplicationException("Unsupported codec.");
      }

      pCodecContext[0] = ffmpeg.avcodec_alloc_context3(pCodec);
      if (pCodecContext[0] == null) {
        Dispose();
        throw new ApplicationException("Could not allocate codec context.");
      }

      if (ffmpeg.avcodec_parameters_to_context(pCodecContext[0], pStream->codecpar) < 0) {
        Dispose();
        throw new ApplicationException("Could not copy codec parameters to context.");
      }

      if (ffmpeg.avcodec_open2(pCodecContext[0], pCodec, null) < 0) {
        Dispose();
        throw new ApplicationException("Could not open codec.");
      }

      pFrame[0] = ffmpeg.av_frame_alloc();
      if (pFrame[0] == null) {
        Dispose();
        throw new ApplicationException("Could not allocate video frame.");
      }

      pRgbFrame[0] = ffmpeg.av_frame_alloc();
      if (pRgbFrame[0] == null) {
        Dispose();
        throw new ApplicationException("Could not allocate RGB frame.");
      }

      pPacket[0] = ffmpeg.av_packet_alloc();
      if (pPacket[0] == null) {
        Dispose();
        throw new ApplicationException("Could not allocate packet.");
      }

      pParser[0] = ffmpeg.av_parser_init((int)pCodec->id);
      if (pParser[0] == null) {
        Dispose();
        throw new ApplicationException("Could not initialize parser.");
      }

      pSwsContext[0] = ffmpeg.sws_getContext(
          pCodecContext[0]->width,
          pCodecContext[0]->height,
          pCodecContext[0]->pix_fmt,
          pCodecContext[0]->width,
          pCodecContext[0]->height,
          AVPixelFormat.AV_PIX_FMT_BGR24,
          ffmpeg.SWS_BILINEAR,
          null, null, null
      );

      if (pSwsContext[0] == null) {
        Dispose();
        throw new ApplicationException("Could not initialize the conversion context.");
      }

      int numBytes = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, pCodecContext[0]->width, pCodecContext[0]->height, 1);
      convertedFrameBufferPtr = Marshal.AllocHGlobal(numBytes);
      dstData = new() ;
      dstLinesize = new ();

      ffmpeg.av_image_fill_arrays(ref dstData, ref dstLinesize, (byte*)convertedFrameBufferPtr, AVPixelFormat.AV_PIX_FMT_BGR24, pCodecContext[0]->width, pCodecContext[0]->height, 1);
    }

    public bool TryGetNextFrame(out Bitmap? frame, out TimeSpan duration) {
      frame = null;
      duration = TimeSpan.Zero;

      fixed (AVPacket** packetPtr = &pPacket[0]) {
        while (ffmpeg.av_read_frame(pFormatContext[0], *packetPtr) >= 0) {
          if (pPacket[0]->stream_index == pStream->index) {
            if (ffmpeg.avcodec_send_packet(pCodecContext[0], pPacket[0]) < 0) {
              ffmpeg.av_packet_unref(pPacket[0]);
              continue;
            }

            while (ffmpeg.avcodec_receive_frame(pCodecContext[0], pFrame[0]) == 0) {
              ffmpeg.sws_scale(
                  pSwsContext[0],
                  pFrame[0]->data,
                  pFrame[0]->linesize,
                  0,
                  pCodecContext[0]->height,
                  this.dstData,
                  this.dstLinesize
              );

              this.pRgbFrame[0]->data.UpdateFrom(this.dstData);
              this.pRgbFrame[0]->linesize.UpdateFrom(this.dstLinesize);

              var width = pCodecContext[0]->width;
              var height = pCodecContext[0]->height;

              frame = new Bitmap(width, height, PixelFormat.Format24bppRgb);
              var bitmapData = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, frame.PixelFormat);

              var src = dstData[0];
              var dst = (byte*)bitmapData.Scan0;
              var srcStride = dstLinesize[0];
              var dstStride = bitmapData.Stride;

              if(srcStride==dstStride)
                Buffer.MemoryCopy(src,dst,dstStride*height,srcStride*height);
              else
                for (var y = 0; y < height; y++) {
                  Buffer.MemoryCopy(src, dst, dstStride, srcStride);
                  src += srcStride;
                  dst += dstStride;
                }

              frame.UnlockBits(bitmapData);

              long pts = pPacket[0]->pts == ffmpeg.AV_NOPTS_VALUE ? previousPts + 1 : pPacket[0]->pts;
              duration = TimeSpan.FromSeconds((pts - previousPts) * ffmpeg.av_q2d(pStream->time_base));
              previousPts = pts;

              ffmpeg.av_packet_unref(pPacket[0]);
              return true;
            }
          }

          ffmpeg.av_packet_unref(pPacket[0]);
        }
      }

      return false;
    }

    public void Dispose() {
      if (pParser[0] != null) {
        fixed (AVCodecParserContext** pParserPtr = &pParser[0]) {
          ffmpeg.av_parser_close(*pParserPtr);
        }
        pParser[0] = null;
      }

      if (pCodecContext[0] != null) {
        fixed (AVCodecContext** pCodecContextPtr = &pCodecContext[0]) {
          ffmpeg.avcodec_free_context(pCodecContextPtr);
        }
        pCodecContext[0] = null;
      }

      if (pFrame[0] != null) {
        fixed (AVFrame** pFramePtr = &pFrame[0]) {
          ffmpeg.av_frame_free(pFramePtr);
        }
        pFrame[0] = null;
      }

      if (pRgbFrame[0] != null) {
        fixed (AVFrame** pRgbFramePtr = &pRgbFrame[0]) {
          ffmpeg.av_frame_free(pRgbFramePtr);
        }
        pRgbFrame[0] = null;
      }

      if (pPacket[0] != null) {
        fixed (AVPacket** pPacketPtr = &pPacket[0]) {
          ffmpeg.av_packet_free(pPacketPtr);
        }
        pPacket[0] = null;
      }

      if (pFormatContext[0] != null) {
        fixed (AVFormatContext** pFormatContextPtr = &pFormatContext[0]) {
          ffmpeg.avformat_close_input(pFormatContextPtr);
        }
        pFormatContext[0] = null;
      }

      if (pSwsContext[0] != null) {
        ffmpeg.sws_freeContext(pSwsContext[0]);
        pSwsContext[0] = null;
      }
    }
    
  }

  public static IEnumerable<(Bitmap frame, TimeSpan duration)> GetFrames(FileInfo video) {
    using var state = new FFmpegState(video);
    while (state.TryGetNextFrame(out var frame, out var duration))
      yield return (frame, duration);
  }
}
