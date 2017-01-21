﻿//------------------------------------------------------------------------------
//	<auto-generated>
//		This code was generated from a template.
//		Manual changes to this file will be overwritten if the code is regenerated.
//	</auto-generated>
//------------------------------------------------------------------------------

using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	unsafe internal interface IConvolver8bpc
	{
		void ConvolveSourceLine(byte* istart, int* tstart, int tstride, int tlen, int* mapxstart, int* mapxastart, int smapx);
		void WriteDestLine(int* tstart, int tstride, byte* ostart, int ox, int ow, int* pmapy, int* pmapya, int smapy);
		void SharpenLine(byte* cstart, byte* bstart, byte* ostart, int ox, int ow, int amt, int thresh);
	}

	unsafe internal interface IConvolver16bpc
	{
		void ConvolveSourceLine(ushort* istart, int* tstart, int tstride, int tlen, int* mapxstart, int* mapxastart, int smapx);
		void WriteDestLine(int* tstart, int tstride, ushort* ostart, int ox, int ow, int* pmapy, int* pmapya, int smapy);
		void SharpenLine(ushort* cstart, ushort* bstart, ushort* ostart, int ox, int ow, int amt, int thresh);
	}

	internal class WicConvolution8bpc : WicBitmapSourceBase
	{
		protected bool BufferSource;
		protected byte[] LineBuff;
		protected int[] IntBuff;
		protected int IntStride;
		protected int IntStartLine;
		protected uint OutWidth;
		protected uint OutHeight;
		protected KernelMap XMap;
		protected KernelMap YMap;
		protected KernelMap XMapAlpha;
		protected KernelMap YMapAlpha;
		protected WICRect SourceRect;
		protected IConvolver8bpc Processor;

		public WicConvolution8bpc(IWICBitmapSource source, KernelMap mapx, KernelMap mapy, bool bufferSource = false) : base(source)
		{
			BufferSource = bufferSource;
			XMap = mapx;
			YMap = mapy;
			XMapAlpha = mapx.MakeAlphaMap();
			YMapAlpha = mapy.MakeAlphaMap();
			OutWidth = (uint)mapx.OutPixelCount;
			OutHeight = (uint)mapy.OutPixelCount;
			SourceRect = new WICRect() { Width = (int)Width, Height = 1 };

			if (Format == Consts.GUID_WICPixelFormat32bppPBGRA)
				Processor = new ConvolverBgra8bpc();
			else if (Format == Consts.GUID_WICPixelFormat32bppBGRA)
				Processor = new ConvolverBgra8bpc();
			else if (Format == Consts.GUID_WICPixelFormat24bppBGR)
				Processor = new ConvolverBgr8bpc();
			else if (Format == Consts.GUID_WICPixelFormat8bppGray)
				Processor = new ConvolverGrey8bpc();
			else if (Format == Consts.GUID_WICPixelFormat8bppY)
				Processor = new ConvolverGrey8bpc();
			else
				throw new NotSupportedException("Unsupported pixel format");

			Stride /= sizeof(byte);
			IntStride = (int)(mapy.SampleCount * Channels);

			LineBuff = new byte[(bufferSource ? mapy.SampleCount : 1) * Stride];
			IntBuff = new int[OutWidth * IntStride];
			IntStartLine = -mapy.SampleCount;
		}

		public override void GetSize(out uint puiWidth, out uint puiHeight)
		{
			puiWidth = OutWidth;
			puiHeight = OutHeight;
		}

		unsafe public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = LineBuff)
			fixed (int* mapystart = YMap.Map, mapxstart = XMap.Map, mapxastart = XMapAlpha.Map, mapyastart = YMapAlpha.Map)
			fixed (int* tstart = IntBuff)
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.SampleCount;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = mapystart + ((oy + y) * (smapy + 1));
					int* pmapya = mapyastart + ((oy + y) * (smapy + 1)) + 1;
					int iy = *pmapy++;
					LoadBuffer(bstart, tstart, mapxstart, mapxastart, iy);

					void* op = (byte*)pbBuffer + y * cbStride;
					ConvolveLine(bstart, tstart, (byte*)op, pmapy, pmapya, smapy, ox, oy + y, ow);
				}
			}
		}

		unsafe protected virtual void ConvolveLine(byte* bstart, int* tstart, byte* ostart, int* pmapy, int* pmapya, int smapy, int ox, int oy, int ow)
		{
			Processor.WriteDestLine(tstart, IntStride, ostart, ox, ow, pmapy, pmapya, smapy);
		}

		unsafe protected void LoadBuffer(byte* bstart, int* tstart, int* mapxstart, int* mapxastart, int iy)
		{
			int smapy = YMap.SampleCount;

			if (iy < IntStartLine)
				IntStartLine = iy - YMap.SampleCount;

			int tc = Math.Min(iy - IntStartLine, smapy);
			if (tc > 0)
			{
				IntStartLine = iy;

				int tk = smapy - tc;
				if (tk > 0)
				{
					if (BufferSource)
						Buffer.MemoryCopy(bstart + tc * Stride, bstart, LineBuff.LongLength * sizeof(byte), tk * Stride * sizeof(byte));

					Buffer.MemoryCopy(tstart + tc * Channels, tstart, IntBuff.LongLength * sizeof(int), (IntBuff.LongLength - tc * Channels) * sizeof(int));
				}

				for (int ty = tk; ty < smapy; ty++)
				{
					byte* bline = BufferSource ? bstart + ty * Stride : bstart;
					int* tline = tstart + ty * Channels;

					SourceRect.Y = iy + ty;
					Source.CopyPixels(SourceRect, Stride * sizeof(byte), Stride * sizeof(byte), (IntPtr)bline);

					Processor.ConvolveSourceLine(bline, tline, IntStride, IntBuff.Length, mapxstart, mapxastart, XMap.SampleCount);
				}
			}
		}
	}

	internal class WicUnsharpMask8bpc : WicConvolution8bpc
	{
		private UnsharpMaskSettings sharpenSettings;
		private byte[] blurBuff;

		public WicUnsharpMask8bpc(IWICBitmapSource source, KernelMap mapx, KernelMap mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			blurBuff = new byte[Stride];
		}

		unsafe protected override void ConvolveLine(byte* bstart, int* tstart, byte* ostart, int* pmapy, int* pmapya, int smapy, int ox, int oy, int ow)
		{
			fixed (byte* blurstart = blurBuff)
			{
				Processor.WriteDestLine(tstart, IntStride, blurstart, ox, ow, pmapy, pmapya, smapy);

				int by = (int)Height - 1 - oy;
				int cy = smapy / 2;
				if (cy > oy)
					cy = oy;
				else if (cy > by)
					cy += cy - by;

				Processor.SharpenLine(bstart + cy * Stride, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold);
			}
		}
	}

	internal class WicConvolution16bpc : WicBitmapSourceBase
	{
		protected bool BufferSource;
		protected ushort[] LineBuff;
		protected int[] IntBuff;
		protected int IntStride;
		protected int IntStartLine;
		protected uint OutWidth;
		protected uint OutHeight;
		protected KernelMap XMap;
		protected KernelMap YMap;
		protected KernelMap XMapAlpha;
		protected KernelMap YMapAlpha;
		protected WICRect SourceRect;
		protected IConvolver16bpc Processor;

		public WicConvolution16bpc(IWICBitmapSource source, KernelMap mapx, KernelMap mapy, bool bufferSource = false) : base(source)
		{
			BufferSource = bufferSource;
			XMap = mapx;
			YMap = mapy;
			XMapAlpha = mapx.MakeAlphaMap();
			YMapAlpha = mapy.MakeAlphaMap();
			OutWidth = (uint)mapx.OutPixelCount;
			OutHeight = (uint)mapy.OutPixelCount;
			SourceRect = new WICRect() { Width = (int)Width, Height = 1 };

			if (Format == Consts.GUID_WICPixelFormat64bppBGRA)
				Processor = new ConvolverBgra16bpc();
			else if (Format == Consts.GUID_WICPixelFormat48bppBGR)
				Processor = new ConvolverBgr16bpc();
			else if (Format == Consts.GUID_WICPixelFormat16bppGray)
				Processor = new ConvolverGrey16bpc();
			else
				throw new NotSupportedException("Unsupported pixel format");

			Stride /= sizeof(ushort);
			IntStride = (int)(mapy.SampleCount * Channels);

			LineBuff = new ushort[(bufferSource ? mapy.SampleCount : 1) * Stride];
			IntBuff = new int[OutWidth * IntStride];
			IntStartLine = -mapy.SampleCount;
		}

		public override void GetSize(out uint puiWidth, out uint puiHeight)
		{
			puiWidth = OutWidth;
			puiHeight = OutHeight;
		}

		unsafe public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (ushort* bstart = LineBuff)
			fixed (int* mapystart = YMap.Map, mapxstart = XMap.Map, mapxastart = XMapAlpha.Map, mapyastart = YMapAlpha.Map)
			fixed (int* tstart = IntBuff)
			{
				int oh = prc.Height, ow = prc.Width, ox = prc.X, oy = prc.Y;
				int smapy = YMap.SampleCount;

				for (int y = 0; y < oh; y++)
				{
					int* pmapy = mapystart + ((oy + y) * (smapy + 1));
					int* pmapya = mapyastart + ((oy + y) * (smapy + 1)) + 1;
					int iy = *pmapy++;
					LoadBuffer(bstart, tstart, mapxstart, mapxastart, iy);

					void* op = (byte*)pbBuffer + y * cbStride;
					ConvolveLine(bstart, tstart, (ushort*)op, pmapy, pmapya, smapy, ox, oy + y, ow);
				}
			}
		}

		unsafe protected virtual void ConvolveLine(ushort* bstart, int* tstart, ushort* ostart, int* pmapy, int* pmapya, int smapy, int ox, int oy, int ow)
		{
			Processor.WriteDestLine(tstart, IntStride, ostart, ox, ow, pmapy, pmapya, smapy);
		}

		unsafe protected void LoadBuffer(ushort* bstart, int* tstart, int* mapxstart, int* mapxastart, int iy)
		{
			int smapy = YMap.SampleCount;

			if (iy < IntStartLine)
				IntStartLine = iy - YMap.SampleCount;

			int tc = Math.Min(iy - IntStartLine, smapy);
			if (tc > 0)
			{
				IntStartLine = iy;

				int tk = smapy - tc;
				if (tk > 0)
				{
					if (BufferSource)
						Buffer.MemoryCopy(bstart + tc * Stride, bstart, LineBuff.LongLength * sizeof(ushort), tk * Stride * sizeof(ushort));

					Buffer.MemoryCopy(tstart + tc * Channels, tstart, IntBuff.LongLength * sizeof(int), (IntBuff.LongLength - tc * Channels) * sizeof(int));
				}

				for (int ty = tk; ty < smapy; ty++)
				{
					ushort* bline = BufferSource ? bstart + ty * Stride : bstart;
					int* tline = tstart + ty * Channels;

					SourceRect.Y = iy + ty;
					Source.CopyPixels(SourceRect, Stride * sizeof(ushort), Stride * sizeof(ushort), (IntPtr)bline);

					Processor.ConvolveSourceLine(bline, tline, IntStride, IntBuff.Length, mapxstart, mapxastart, XMap.SampleCount);
				}
			}
		}
	}

	internal class WicUnsharpMask16bpc : WicConvolution16bpc
	{
		private UnsharpMaskSettings sharpenSettings;
		private ushort[] blurBuff;

		public WicUnsharpMask16bpc(IWICBitmapSource source, KernelMap mapx, KernelMap mapy, UnsharpMaskSettings ss) : base(source, mapx, mapy, true)
		{
			sharpenSettings = ss;
			blurBuff = new ushort[Stride];
		}

		unsafe protected override void ConvolveLine(ushort* bstart, int* tstart, ushort* ostart, int* pmapy, int* pmapya, int smapy, int ox, int oy, int ow)
		{
			fixed (ushort* blurstart = blurBuff)
			{
				Processor.WriteDestLine(tstart, IntStride, blurstart, ox, ow, pmapy, pmapya, smapy);

				int by = (int)Height - 1 - oy;
				int cy = smapy / 2;
				if (cy > oy)
					cy = oy;
				else if (cy > by)
					cy += cy - by;

				Processor.SharpenLine(bstart + cy * Stride, blurstart, ostart, ox, ow, sharpenSettings.Amount, sharpenSettings.Threshold);
			}
		}
	}

}
