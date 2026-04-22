using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;

namespace PrimevalTitmouse
{
  /// <summary>
  /// Draw helpers for custom HUD status bars (hunger, thirst, etc).
  /// </summary>
  internal class StatusBars
  {
    public static Color barBackgroundColor = Color.Black;
    public static Color barBackgroundTick = new Color(120, 120, 120);
    public static Color barBorderColor = Color.DarkGoldenrod;
    public static int barBorderWidth = 2;
    public static Color barForegroundColor = new Color(150, 150, 150);
    public static Color barForegroundTick = new Color(50, 50, 50);
    public static int barHeight = 204;
    public static int barWidth = 24;
    public static Texture2D barBackground;
    public static Texture2D barForeground;
    private static Texture2D statusBarsTexture;
    private static Texture2D debugBarsTexture;
    private static IModHelper helper;
    private static IMonitor monitor;
    private static bool loggedCustomLoadFailure;
    private static int sourceBarWidth;
    private static int sourceBarHeight;
    private static int drawScale = 4;
    private static bool statusBarsHasMask;
    private static bool debugBarsHasMask;
    private static int statusBarsFrameHeight;
    private static int debugBarsFrameHeight;

    // Mask-derived per-column fill bounds (in source pixels, frame-local coords).
    // Index 0 = left bar, 1 = right bar.
    private static short[][] statusTopY;
    private static short[][] statusBottomY;
    private static short[][] debugTopY;
    private static short[][] debugBottomY;

    public static void Initialize(IModHelper modHelper, IMonitor modMonitor)
    {
      helper = modHelper;
      monitor = modMonitor;
      EnsureCustomTexturesLoaded();
    }

    private static void EnsureCustomTexturesLoaded()
    {
      if (helper == null || (statusBarsTexture != null && debugBarsTexture != null))
        return;

      try
      {
        statusBarsTexture = helper.ModContent.Load<Texture2D>("Assets/StatusBars.png");
        debugBarsTexture = helper.ModContent.Load<Texture2D>("Assets/Debug.png");

        int centerGap = statusBarsTexture.Width % 2;
        sourceBarWidth = (statusBarsTexture.Width - centerGap) / 2;

        statusBarsHasMask = statusBarsTexture.Height % 2 == 0;
        statusBarsFrameHeight = statusBarsHasMask ? statusBarsTexture.Height / 2 : statusBarsTexture.Height;
        debugBarsHasMask = debugBarsTexture.Height % 2 == 0;
        debugBarsFrameHeight = debugBarsHasMask ? debugBarsTexture.Height / 2 : debugBarsTexture.Height;

        // Use the larger frame height for layout so bottoms can align consistently.
        sourceBarHeight = Math.Max(statusBarsFrameHeight, debugBarsFrameHeight);

        // Match vanilla HUD pixel scaling.
        drawScale = Math.Max(1, Game1.pixelZoom);
        barWidth = sourceBarWidth * drawScale;
        barHeight = sourceBarHeight * drawScale;

        if (statusBarsHasMask)
          BuildMaskColumns(statusBarsTexture, statusBarsFrameHeight, out statusTopY, out statusBottomY);
        if (debugBarsHasMask)
          BuildMaskColumns(debugBarsTexture, debugBarsFrameHeight, out debugTopY, out debugBottomY);
      }
      catch (Exception ex)
      {
        if (!loggedCustomLoadFailure && monitor != null)
        {
          monitor.Log($"Could not load custom bar textures from Assets/StatusBars.png and Assets/Debug.png. Using fallback bars. Details: {ex.Message}", LogLevel.Warn);
          loggedCustomLoadFailure = true;
        }
      }
    }

    private static Rectangle GetBarSource(Texture2D texture, bool useRightHalf)
    {
      int centerGap = texture.Width % 2;
      int halfWidth = (texture.Width - centerGap) / 2;
      int startX = useRightHalf ? halfWidth + centerGap : 0;
      int frameH = (texture == statusBarsTexture && statusBarsHasMask) ? statusBarsFrameHeight
                 : (texture == debugBarsTexture && debugBarsHasMask) ? debugBarsFrameHeight
                 : texture.Height;
      return new Rectangle(startX, 0, halfWidth, frameH);
    }

    private static Rectangle GetMaskSource(Texture2D texture, bool useRightHalf)
    {
      int centerGap = texture.Width % 2;
      int halfWidth = (texture.Width - centerGap) / 2;
      int startX = useRightHalf ? halfWidth + centerGap : 0;
      int frameH = (texture == statusBarsTexture && statusBarsHasMask) ? statusBarsFrameHeight
                 : (texture == debugBarsTexture && debugBarsHasMask) ? debugBarsFrameHeight
                 : texture.Height;
      return new Rectangle(startX, frameH, halfWidth, frameH);
    }

    private static void BuildMaskColumns(Texture2D texture, int frameHeight, out short[][] topY, out short[][] bottomY)
    {
      // Read full texture once.
      Color[] pixels = new Color[texture.Width * texture.Height];
      texture.GetData(pixels);

      int centerGap = texture.Width % 2;
      int halfW = (texture.Width - centerGap) / 2;
      topY = new short[2][];
      bottomY = new short[2][];
      topY[0] = new short[halfW];
      topY[1] = new short[halfW];
      bottomY[0] = new short[halfW];
      bottomY[1] = new short[halfW];

      for (int bar = 0; bar < 2; bar++)
      {
        int startX = bar == 1 ? halfW + centerGap : 0;
        for (int x = 0; x < halfW; x++)
        {
          int minY = int.MaxValue;
          int maxY = int.MinValue;
          int srcX = startX + x;

          // Scan only the mask row (lower half).
          for (int y = frameHeight; y < frameHeight * 2; y++)
          {
            Color c = pixels[srcX + y * texture.Width];
            if (c.A > 0)
            {
              int localY = y - frameHeight;
              if (localY < minY) minY = localY;
              if (localY > maxY) maxY = localY;
            }
          }

          if (minY == int.MaxValue)
          {
            // No fill pixels in this column: mark as empty.
            topY[bar][x] = -1;
            bottomY[bar][x] = -1;
          }
          else
          {
            topY[bar][x] = (short)minY;
            bottomY[bar][x] = (short)maxY;
          }
        }
      }
    }

    /// <summary>
    /// Lazily creates reusable bar textures used by all status meters.
    /// </summary>
    private static void CreateTextures()
    {
      StatusBars.barBackground = new Texture2D(((GraphicsDeviceManager) Game1.graphics).GraphicsDevice, StatusBars.barWidth, StatusBars.barHeight);
      StatusBars.barForeground = new Texture2D(((GraphicsDeviceManager) Game1.graphics).GraphicsDevice, StatusBars.barWidth, StatusBars.barHeight);
      Color[] data1 = new Color[StatusBars.barHeight * StatusBars.barWidth];
      Color[] data2 = new Color[StatusBars.barHeight * StatusBars.barWidth];
      for (int index1 = 0; index1 < StatusBars.barWidth; ++index1)
      {
        for (int index2 = 0; index2 < StatusBars.barHeight; ++index2)
        {
          Color color1 = StatusBars.barBackgroundColor;
          Color color2 = StatusBars.barForegroundColor;
          bool flag1 = index1 + 1 <= StatusBars.barBorderWidth || index1 >= StatusBars.barWidth - StatusBars.barBorderWidth;
          bool flag2 = index2 + 1 <= StatusBars.barBorderWidth || index2 >= StatusBars.barHeight - StatusBars.barBorderWidth;
          if (flag1 | flag2)
          {
            color1 = StatusBars.barBorderColor;
            color2 = Color.Transparent;
            if (flag1 & flag2)
              color1 = Color.Transparent;
          }
          if (!flag1)
          {
            float scale = new float[10]
            {
              1f,
              1.3f,
              1.7f,
              2f,
              1.9f,
              1.5f,
              1.3f,
              1f,
              0.8f,
              0.4f
            }[(int) ((double) index1 * 10.0 / (double) StatusBars.barWidth)];
            color1 = Color.Multiply(color1, scale);
            color2 = Color.Multiply(color2, scale);
          }
          data1[index1 + index2 * StatusBars.barWidth] = color1;
          data2[index1 + index2 * StatusBars.barWidth] = color2;
        }
      }
      StatusBars.barBackground.SetData<Color>(data1);
      StatusBars.barForeground.SetData<Color>(data2);
    }

        /// <summary>
        /// Draws one vertical filled status bar.
        /// </summary>
        public static void DrawStatusBar(int x, int y, float percentage, Color color)
        {
            SpriteBatch spriteBatch = (SpriteBatch)Game1.spriteBatch;
            EnsureCustomTexturesLoaded();

            if (Game1.eventUp || Game1.farmEvent != null)
            {
                return;
            }

            if (statusBarsTexture != null)
            {
              DrawSpriteBar(spriteBatch, statusBarsTexture, x, y, percentage, false, color);
              return;
            }

            if (StatusBars.barBackground == null || StatusBars.barForeground == null)
                StatusBars.CreateTextures();

            percentage = Math.Min(percentage, 1f);
            Rectangle destinationRectangle = new Rectangle(x, y, StatusBars.barWidth, StatusBars.barHeight);
            spriteBatch.Draw(StatusBars.barBackground, destinationRectangle, new Rectangle?(new Rectangle(0, 0, StatusBars.barWidth, StatusBars.barHeight)), Color.White);
            int height = (int)((double)(destinationRectangle.Height - StatusBars.barBorderWidth * 2) * (double)percentage);
            destinationRectangle.Y = destinationRectangle.Y + destinationRectangle.Height - height - StatusBars.barBorderWidth;
            destinationRectangle.Height = height;
            spriteBatch.Draw(StatusBars.barForeground, destinationRectangle, new Rectangle?(new Rectangle(0, 0, StatusBars.barWidth, height)), color);
        }

        public static void DrawStatusBar(int x, int y, float percentage, bool debugTexture, bool useRightHalf, Color fillColor)
        {
            SpriteBatch spriteBatch = (SpriteBatch)Game1.spriteBatch;
            EnsureCustomTexturesLoaded();

            if (Game1.eventUp || Game1.farmEvent != null)
            {
                return;
            }

            Texture2D texture = debugTexture ? debugBarsTexture : statusBarsTexture;
            if (texture == null)
            {
                DrawStatusBar(x, y, percentage, fillColor);
                return;
            }

            DrawSpriteBar(spriteBatch, texture, x, y, percentage, useRightHalf, fillColor);
        }

        private static void DrawSpriteBar(SpriteBatch spriteBatch, Texture2D texture, int x, int y, float percentage, bool useRightHalf, Color fillColor)
        {
            percentage = Math.Clamp(percentage, 0f, 1f);
            int scale = drawScale <= 0 ? Math.Max(1, Game1.pixelZoom) : drawScale;
            Rectangle frameSource = GetBarSource(texture, useRightHalf);
            Rectangle destination = new Rectangle(x, y, frameSource.Width * scale, frameSource.Height * scale);

            bool hasStatusMask = texture == statusBarsTexture && statusBarsHasMask && statusTopY != null && statusBottomY != null;
            bool hasDebugMask = texture == debugBarsTexture && debugBarsHasMask && debugTopY != null && debugBottomY != null;
            bool hasMask = hasStatusMask || hasDebugMask;
            if (hasMask)
            {
              // Draw frame first. We'll draw the dynamic fill on top, clipped by the mask,
              // so any baked interior colors in the art don't hide the fill.
              // If this texture's frame is shorter than our layout height, bottom-align it.
              int frameHScaled = frameSource.Height * scale;
              int layoutHScaled = sourceBarHeight * scale;
              if (frameHScaled < layoutHScaled)
                destination.Y += (layoutHScaled - frameHScaled);

              spriteBatch.Draw(texture, destination, frameSource, Color.White);

              int barIndex = useRightHalf ? 1 : 0;
              int halfW = frameSource.Width;
              short[][] topArr = hasStatusMask ? statusTopY : debugTopY;
              short[][] bottomArr = hasStatusMask ? statusBottomY : debugBottomY;
              int frameHeight = hasStatusMask ? statusBarsFrameHeight : debugBarsFrameHeight;

              // Draw interior background and fill per column using the mask bounds.
              for (int col = 0; col < halfW; col++)
              {
                int top = topArr[barIndex][col];
                int bottom = bottomArr[barIndex][col];
                if (top < 0 || bottom < 0 || bottom < top)
                  continue;

                int allowedH = bottom - top + 1;

                // background
                Rectangle bg = new Rectangle(
                  destination.X + col * scale,
                  destination.Y + top * scale,
                  scale,
                  allowedH * scale);
                spriteBatch.Draw(Game1.staminaRect, bg, Color.Black * 0.35f);

                int filled = (int)Math.Round(allowedH * percentage);
                if (filled <= 0)
                  continue;

                Rectangle fg = new Rectangle(
                  destination.X + col * scale,
                  destination.Y + (bottom - filled + 1) * scale,
                  scale,
                  filled * scale);
                spriteBatch.Draw(Game1.staminaRect, fg, fillColor);
              }
            }
            else
            {
              // Fallback: fill the whole interior rectangle (legacy behavior for Debug.png until it has a mask).
              spriteBatch.Draw(texture, destination, frameSource, Color.White);

              Rectangle inner = new Rectangle(destination.X + 2 * scale, destination.Y + 9 * scale, Math.Max(1, destination.Width - 4 * scale), Math.Max(1, destination.Height - 12 * scale));
              spriteBatch.Draw(Game1.staminaRect, inner, Color.Black * 0.35f);
              int filledH = (int)Math.Round(inner.Height * percentage);
              if (filledH > 0)
              {
                Rectangle filled = new Rectangle(inner.X, inner.Bottom - filledH, inner.Width, filledH);
                spriteBatch.Draw(Game1.staminaRect, filled, fillColor);
              }
            }
        }
  }
}
