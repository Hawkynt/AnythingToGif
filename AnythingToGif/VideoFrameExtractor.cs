using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FFmpeg.AutoGen;

public static class VideoFrameExtractor {

  private unsafe struct FFmpegState : IDisposable {
    private readonly AVFormatContext*[] pFormatContext = new AVFormatContext*[1];
    private readonly AVCodecContext*[] pCodecContext = new AVCodecContext*[1];
    private readonly AVStream* pStream;
    private readonly AVFrame*[] pFrame = new AVFrame*[1];

    private long previousPts = 0;

    public FFmpegState(FileInfo video) {

      fixed (AVFormatContext** x = &this.pFormatContext[0])
        // TODO: specified method is not supported
        if (ffmpeg.avformat_open_input(x, video.FullName, null, null) != 0)
          throw new ApplicationException("Could not open file.");

      if (ffmpeg.avformat_find_stream_info(this.pFormatContext[0], null) != 0) {
        fixed (AVFormatContext** x = &this.pFormatContext[0])
          ffmpeg.avformat_close_input(x);

        throw new ApplicationException("Could not find stream info.");
      }

      this.pStream = null;
      for (var i = 0; i < this.pFormatContext[0]->nb_streams; ++i) {
        var avStream = this.pFormatContext[0]->streams[i];
        if (avStream->codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_VIDEO)
          continue;

        this.pStream = avStream;
        break;
      }

      if (this.pStream == null) {
        fixed (AVFormatContext** x = &this.pFormatContext[0])
          ffmpeg.avformat_close_input(x);

        throw new ApplicationException("Could not find video stream.");
      }

      var pCodec = ffmpeg.avcodec_find_decoder(this.pStream->codecpar->codec_id);
      if (pCodec == null) {
        fixed (AVFormatContext** x = &this.pFormatContext[0])
          ffmpeg.avformat_close_input(x);

        throw new ApplicationException("Unsupported codec.");
      }

      this.pCodecContext[0] = ffmpeg.avcodec_alloc_context3(pCodec);
      if (this.pCodecContext == null) {
        fixed (AVFormatContext** x = &this.pFormatContext[0])
          ffmpeg.avformat_close_input(x);

        throw new ApplicationException("Could not allocate codec context.");
      }

      if (ffmpeg.avcodec_open2(this.pCodecContext[0], pCodec, null) < 0) {

        fixed (AVCodecContext** x = &this.pCodecContext[0])
          ffmpeg.avcodec_free_context(x);

        fixed (AVFormatContext** x = &this.pFormatContext[0])
          ffmpeg.avformat_close_input(x);

        throw new ApplicationException("Could not open codec.");
      }

      this.pFrame[0] = ffmpeg.av_frame_alloc();
    }

    public bool TryGetNextFrame(out Bitmap? frame, out TimeSpan duration) {
      AVPacket[] p = [new()];
      var timePerFrame = ffmpeg.av_q2d(this.pStream->time_base);

      fixed (AVPacket* packet = &p[0])
        while (ffmpeg.av_read_frame(this.pFormatContext[0], packet) >= 0) {

          if (packet->stream_index != this.pStream->index) {
            ffmpeg.av_packet_free(&packet);
            continue;
          }

          var gotFrame = ffmpeg.avcodec_send_packet(this.pCodecContext[0], packet);
          gotFrame &= ffmpeg.avcodec_receive_frame(this.pCodecContext[0], this.pFrame[0]);
          if (gotFrame == 0) {
            ffmpeg.av_packet_free(&packet);
            continue;
          }

          var width = this.pCodecContext[0]->width;
          var height = this.pCodecContext[0]->height;

          frame = new(width, height, PixelFormat.Format24bppRgb);
          var bitmapData = frame.LockBits(new(0, 0, width, height), ImageLockMode.WriteOnly, frame.PixelFormat);

          var src = this.pFrame[0]->data[0];
          var dst = (byte*)bitmapData.Scan0;

          var srcStride = width * 3;
          var dstStride = bitmapData.Stride;

          if (srcStride == dstStride)
            new Span<byte>(src, srcStride * height).CopyTo(new(dst, dstStride * height));
          else
            for (var y = 0; y < height; y++) {
              var spanSrc = new Span<byte>(src, srcStride);
              var spanDst = new Span<byte>(dst, bitmapData.Stride);
              spanSrc.CopyTo(spanDst);

              src += srcStride;
              dst += dstStride;
            }

          frame.UnlockBits(bitmapData);

          var pts = packet->pts == ffmpeg.AV_NOPTS_VALUE ? this.previousPts + 1 : packet->pts;
          duration = TimeSpan.FromSeconds((pts - this.previousPts) * timePerFrame);
          this.previousPts = pts;

          ffmpeg.av_packet_free(&packet);
          return true;
        }

      frame = null;
      duration = TimeSpan.Zero;
      return false;
    }

    public void Dispose() {
      fixed (AVFrame** x = &this.pFrame[0])
        ffmpeg.av_frame_free(x);

      fixed (AVCodecContext** x = &this.pCodecContext[0])
        ffmpeg.avcodec_free_context(x);

      fixed (AVFormatContext** x = &this.pFormatContext[0])
        ffmpeg.avformat_close_input(x);

    }
  }

  public static IEnumerable<(Bitmap frame, TimeSpan duration)> GetFrames(FileInfo video) {
    using var state = new FFmpegState(video);
    while (state.TryGetNextFrame(out var frame, out var duration))
      yield return (frame, duration);
  }

}
