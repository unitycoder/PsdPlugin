﻿/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006, Frank Blumenberg
// 
// See License.txt for complete licensing and attribution information.
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
// THE SOFTWARE.
// 
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

using PhotoshopFile;

namespace PaintDotNet.Data.PhotoshopFileType
{
  class ImageDecoderPdn
  {
    public static BitmapLayer DecodeImage(PsdFile psdFile)
    {
      BitmapLayer layer = PaintDotNet.Layer.CreateBackgroundLayer(psdFile.Columns, psdFile.Rows);

      Surface surface = layer.Surface;
      surface.Clear((ColorBgra)0xffffffff);

      for (int y = 0; y < psdFile.Rows; y++)
      {
        int rowIndex = y * psdFile.Columns;

        MemoryBlock dstRow = surface.GetRow(y);

        int dstIndex = 0;

        for (int x = 0; x < psdFile.Columns; x++)
        {
          int pos = rowIndex + x;


          SetPDNColor(dstRow, dstIndex, psdFile, pos);
          dstIndex += 4;
        }
      }

      return layer;
    }

    /////////////////////////////////////////////////////////////////////////// 

    private static void SetPDNColor(MemoryBlock dstRow, int dstIndex, PsdFile psdFile, int pos)
    {
      switch (psdFile.ColorMode)
      {
        case PsdFile.ColorModes.RGB:
          dstRow[dstIndex]     = psdFile.ImageData[2][pos];
          dstRow[dstIndex + 1] = psdFile.ImageData[1][pos];
          dstRow[dstIndex + 2] = psdFile.ImageData[0][pos];
          break;
        case PsdFile.ColorModes.CMYK:
          SetPDNColorCMYK(dstRow, dstIndex,
              psdFile.ImageData[0][pos],
              psdFile.ImageData[1][pos],
              psdFile.ImageData[2][pos],
              psdFile.ImageData[3][pos]);
          break;
        case PsdFile.ColorModes.Multichannel:
          SetPDNColorCMYK(dstRow, dstIndex,
              psdFile.ImageData[0][pos],
              psdFile.ImageData[1][pos],
              psdFile.ImageData[2][pos],
              0);
          break;
        case PsdFile.ColorModes.Grayscale:
        case PsdFile.ColorModes.Duotone:
          dstRow[dstIndex]     = psdFile.ImageData[0][pos];
          dstRow[dstIndex + 1] = psdFile.ImageData[0][pos];
          dstRow[dstIndex + 2] = psdFile.ImageData[0][pos];
          break;
        case PsdFile.ColorModes.Indexed:
          int index = (int)psdFile.ImageData[0][pos];
          dstRow[dstIndex]     = psdFile.ColorModeData[index + 2 * 256];
          dstRow[dstIndex + 1] = psdFile.ColorModeData[index + 256];
          dstRow[dstIndex + 2] = (byte)psdFile.ColorModeData[index];
          break;
        case PsdFile.ColorModes.Lab:
          SetPDNColorLab(dstRow, dstIndex,
            psdFile.ImageData[0][pos],
            psdFile.ImageData[1][pos],
            psdFile.ImageData[2][pos]);
          break;
      }
    }

    /////////////////////////////////////////////////////////////////////////// 

    public static BitmapLayer DecodeImage(PhotoshopFile.Layer psdLayer)
    {
      BitmapLayer pdnLayer = new BitmapLayer(psdLayer.PsdFile.Columns, psdLayer.PsdFile.Rows);

      Surface surface = pdnLayer.Surface;
      surface.Clear((ColorBgra)0);

      try
      {
        bool hasMaskChannel = psdLayer.SortedChannels.ContainsKey(-2);
        var channels = psdLayer.ChannelsArray;
        var alphaChannel = psdLayer.AlphaChannel;

        for (int yPsdLayer = 0; yPsdLayer < psdLayer.Rect.Height; yPsdLayer++)
        {
          if ((yPsdLayer + psdLayer.Rect.Y) >= 0 && (yPsdLayer + psdLayer.Rect.Y) < surface.Height)
          {
            MemoryBlock dstRow = surface.GetRow(yPsdLayer + psdLayer.Rect.Y);

            int srcRowIndex = yPsdLayer * psdLayer.Rect.Width;
            int dstIndex = psdLayer.Rect.Left * 4;

            for (int xPsdLayer = 0; xPsdLayer < psdLayer.Rect.Width && ((xPsdLayer + psdLayer.Rect.Left) < psdLayer.PsdFile.Columns); xPsdLayer++)
            {
              if ((xPsdLayer + psdLayer.Rect.X) >= 0 && (xPsdLayer + psdLayer.Rect.X) < surface.Width)
              {
                int srcIndex = srcRowIndex + xPsdLayer;
                if (dstIndex >= 0)
                {
                  SetPDNColor(dstRow, dstIndex, psdLayer, channels, alphaChannel, srcIndex);
                  int maskAlpha = 255;
                  if (hasMaskChannel)
                  {
                    maskAlpha = GetMaskAlpha(psdLayer.MaskData, xPsdLayer, yPsdLayer);
                  }
                  SetPDNAlpha(dstRow, dstIndex, alphaChannel, srcIndex, maskAlpha);
                }
              }

              dstIndex += 4;
            }
          }
        }
      }
      catch (Exception ex)
      {
        throw ex;
      }

      return pdnLayer;
    }

    /////////////////////////////////////////////////////////////////////////// 
    private static void SetPDNColor(MemoryBlock dstRow, int dstIndex, PhotoshopFile.Layer layer,
      PhotoshopFile.Layer.Channel[] channels, PhotoshopFile.Layer.Channel alphaChannel, int pos)
    {
      switch (layer.PsdFile.ColorMode)
      {
        case PsdFile.ColorModes.RGB:
          dstRow[dstIndex]     = channels[2].ImageData[pos];
          dstRow[dstIndex + 1] = channels[1].ImageData[pos];
          dstRow[dstIndex + 2] = channels[0].ImageData[pos];
          break;
        case PsdFile.ColorModes.CMYK:
          SetPDNColorCMYK(dstRow, dstIndex,
            channels[0].ImageData[pos],
            channels[1].ImageData[pos],
            channels[2].ImageData[pos],
            channels[3].ImageData[pos]);
          break;
        case PsdFile.ColorModes.Multichannel:
          SetPDNColorCMYK(dstRow, dstIndex,
            channels[0].ImageData[pos],
            channels[1].ImageData[pos],
            channels[2].ImageData[pos],
            0);
          break;
        case PsdFile.ColorModes.Grayscale:
        case PsdFile.ColorModes.Duotone:
          dstRow[dstIndex]     = channels[0].ImageData[pos];
          dstRow[dstIndex + 1] = channels[0].ImageData[pos];
          dstRow[dstIndex + 2] = channels[0].ImageData[pos];
          break;
        case PsdFile.ColorModes.Indexed:
          int index = (int)channels[0].ImageData[pos];
          dstRow[dstIndex]     = layer.PsdFile.ColorModeData[index + 2 * 256];
          dstRow[dstIndex + 1] = layer.PsdFile.ColorModeData[index + 256];
          dstRow[dstIndex + 2] = (byte)layer.PsdFile.ColorModeData[index];
          break;
        case PsdFile.ColorModes.Lab:
          SetPDNColorLab(dstRow, dstIndex,
            channels[0].ImageData[pos],
            channels[1].ImageData[pos],
            channels[2].ImageData[pos]);
          break;
      }
    }
    
    private static void SetPDNAlpha(MemoryBlock dstRow, int dstIndex,
      PhotoshopFile.Layer.Channel alphaChannel, int srcIndex, int maskAlpha)
    {
      byte alpha = 255;
      if (alphaChannel != null)
        alpha = alphaChannel.ImageData[srcIndex];
      if (maskAlpha < 255)
        alpha = (byte)(alpha * maskAlpha / 255);

      dstRow[dstIndex + 3] = alpha;
    }

    /////////////////////////////////////////////////////////////////////////// 

    /////////////////////////////////////////////////////////////////////////// 

    private static int GetMaskAlpha(PhotoshopFile.Layer.Mask mask, int x, int y)
    {
      int c = 255;
      try
      {
        if (mask.PositionIsRelative)
        {
          x -= mask.Rect.X;
          y -= mask.Rect.Y;
        }
        else
        {
          x = (x + mask.Layer.Rect.X) - mask.Rect.X;
          y = (y + mask.Layer.Rect.Y) - mask.Rect.Y;
        }

        if (y >= 0 && y < mask.Rect.Height &&
             x >= 0 && x < mask.Rect.Width)
        {
          int pos = y * mask.Rect.Width + x;
          if (pos < mask.ImageData.Length)
            c = mask.ImageData[pos];
          else
            c = 255;
        }
      }
      catch (Exception ex)
      {
        throw ex;
      }

      return c;
    }

    /////////////////////////////////////////////////////////////////////////// 

    private static void SetPDNColorLab(MemoryBlock dstRow, int dstIndex, byte lb, byte ab, byte bb)
    {
      double exL, exA, exB;

      exL = (double)lb;
      exA = (double)ab;
      exB = (double)bb;

      double L_coef, a_coef, b_coef;
      L_coef = 256.0 / 100.0;
      a_coef = 256.0 / 256.0;
      b_coef = 256.0 / 256.0;

      int L = (int)(exL / L_coef);
      int a = (int)(exA / a_coef - 128.0);
      int b = (int)(exB / b_coef - 128.0);

      // For the conversion we first convert values to XYZ and then to RGB
      // Standards used Observer = 2, Illuminant = D65

      const double ref_X = 95.047;
      const double ref_Y = 100.000;
      const double ref_Z = 108.883;

      double var_Y = ((double)L + 16.0) / 116.0;
      double var_X = (double)a / 500.0 + var_Y;
      double var_Z = var_Y - (double)b / 200.0;

      if (Math.Pow(var_Y, 3) > 0.008856)
        var_Y = Math.Pow(var_Y, 3);
      else
        var_Y = (var_Y - 16 / 116) / 7.787;

      if (Math.Pow(var_X, 3) > 0.008856)
        var_X = Math.Pow(var_X, 3);
      else
        var_X = (var_X - 16 / 116) / 7.787;

      if (Math.Pow(var_Z, 3) > 0.008856)
        var_Z = Math.Pow(var_Z, 3);
      else
        var_Z = (var_Z - 16 / 116) / 7.787;

      double X = ref_X * var_X;
      double Y = ref_Y * var_Y;
      double Z = ref_Z * var_Z;

      SetPDNColorXYZ(dstRow, dstIndex, X, Y, Z);
    }

    ////////////////////////////////////////////////////////////////////////////


    private static void SetPDNColorXYZ(MemoryBlock dstRow, int dstIndex, double X, double Y, double Z)
    {
      // Standards used Observer = 2, Illuminant = D65
      // ref_X = 95.047, ref_Y = 100.000, ref_Z = 108.883

      double var_X = X / 100.0;
      double var_Y = Y / 100.0;
      double var_Z = Z / 100.0;

      double var_R = var_X * 3.2406 + var_Y * (-1.5372) + var_Z * (-0.4986);
      double var_G = var_X * (-0.9689) + var_Y * 1.8758 + var_Z * 0.0415;
      double var_B = var_X * 0.0557 + var_Y * (-0.2040) + var_Z * 1.0570;

      if (var_R > 0.0031308)
        var_R = 1.055 * (Math.Pow(var_R, 1 / 2.4)) - 0.055;
      else
        var_R = 12.92 * var_R;

      if (var_G > 0.0031308)
        var_G = 1.055 * (Math.Pow(var_G, 1 / 2.4)) - 0.055;
      else
        var_G = 12.92 * var_G;

      if (var_B > 0.0031308)
        var_B = 1.055 * (Math.Pow(var_B, 1 / 2.4)) - 0.055;
      else
        var_B = 12.92 * var_B;

      int nRed = (int)(var_R * 256.0);
      int nGreen = (int)(var_G * 256.0);
      int nBlue = (int)(var_B * 256.0);

      if (nRed < 0) nRed = 0;
      else if (nRed > 255) nRed = 255;
      if (nGreen < 0) nGreen = 0;
      else if (nGreen > 255) nGreen = 255;
      if (nBlue < 0) nBlue = 0;
      else if (nBlue > 255) nBlue = 255;

      dstRow[dstIndex]     = (byte)nBlue;
      dstRow[dstIndex + 1] = (byte)nGreen;
      dstRow[dstIndex + 2] = (byte)nRed;
    }

    ///////////////////////////////////////////////////////////////////////////////
    //
    // The algorithms for these routines were taken from:
    //     http://www.neuro.sfc.keio.ac.jp/~aly/polygon/info/color-space-faq.html
    //
    // RGB --> CMYK                              CMYK --> RGB
    // ---------------------------------------   --------------------------------------------
    // Black   = minimum(1-Red,1-Green,1-Blue)   Red   = 1-minimum(1,Cyan*(1-Black)+Black)
    // Cyan    = (1-Red-Black)/(1-Black)         Green = 1-minimum(1,Magenta*(1-Black)+Black)
    // Magenta = (1-Green-Black)/(1-Black)       Blue  = 1-minimum(1,Yellow*(1-Black)+Black)
    // Yellow  = (1-Blue-Black)/(1-Black)
    //

    private static void SetPDNColorCMYK(MemoryBlock dstRow, int dstIndex, byte c, byte m, byte y, byte k)
    {
      double C, M, Y, K;

      double exC, exM, exY, exK;
      double dMaxColours = Math.Pow(2, 8);

      exC = (double)c;
      exM = (double)m;
      exY = (double)y;
      exK = (double)k;

      C = (1.0 - exC / dMaxColours);
      M = (1.0 - exM / dMaxColours);
      Y = (1.0 - exY / dMaxColours);
      K = (1.0 - exK / dMaxColours);

      int nRed = (int)((1.0 - (C * (1 - K) + K)) * 255);
      int nGreen = (int)((1.0 - (M * (1 - K) + K)) * 255);
      int nBlue = (int)((1.0 - (Y * (1 - K) + K)) * 255);

      if (nRed < 0) nRed = 0;
      else if (nRed > 255) nRed = 255;
      if (nGreen < 0) nGreen = 0;
      else if (nGreen > 255) nGreen = 255;
      if (nBlue < 0) nBlue = 0;
      else if (nBlue > 255) nBlue = 255;

      dstRow[dstIndex]     = (byte)nBlue;
      dstRow[dstIndex + 1] = (byte)nGreen;
      dstRow[dstIndex + 2] = (byte)nRed;
    }
  }
}