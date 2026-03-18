using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Generates a map image with a coordinate pin for a MapLinkPayload.
/// Loads the zone map texture from game data, draws a red marker at the
/// given raw coordinates, and returns the result as a JPEG byte array
/// sized to <see cref="OutputSize"/> pixels on each side.
/// </summary>
public sealed class MapImageService(IDataManager dataManager, IPluginLog log)
{
    private const int OutputSize = 512;
    private const int PinRadius  = 12;

    /// <summary>
    /// Generates a JPEG map image with a pin at the position encoded in <paramref name="map"/>.
    /// Returns <c>null</c> when the texture cannot be found or decoded.
    /// </summary>
    public byte[]? GeneratePinImage(MapLinkPayload map)
    {
        try
        {
            var lumMap   = map.TerritoryType.Value.Map.Value;
            var mapIdStr = lumMap.Id.ToString(); // e.g. "s1fa/00"
            var slash    = mapIdStr.IndexOf('/');
            if (slash < 0) return null;

            var part1   = mapIdStr[..slash];
            var part2   = mapIdStr[(slash + 1)..];
            var texPath = $"ui/map/{part1}/{part2}/{part1}{part2}_m.tex";

            var texFile = dataManager.GetFile<TexFile>(texPath);
            if (texFile is null)
            {
                log.Warning("[MapImageService] Texture not found: {Path}", texPath);
                return null;
            }

            var width  = texFile.Header.Width;
            var height = texFile.Header.Height;
            // ImageData is B8G8R8A8 (BGRA) — matches ImageSharp's Bgra32 pixel format
            var bgra   = texFile.ImageData;

            using var img = Image.LoadPixelData<Bgra32>(bgra, width, height);

            // Convert raw game coordinates to pixel position.
            // Formula (inverse of Dalamud's ConvertRawPositionToMapCoordinate):
            //   normalised = (RawX + OffsetX + 1024) / 2048
            //   pixel      = normalised * imageWidth
            // Use display coordinates (XCoord/YCoord) + SizeFactor to compute the normalised
            // position on the map image. This is the inverse of Dalamud's formula:
            //   XCoord = (41 / scaleFactor) * normalised + 1
            //   → normalised = (XCoord - 1) * scaleFactor / 41
            var scaleFactor = lumMap.SizeFactor / 100f;
            var normX = (map.XCoord - 1f) * scaleFactor / 41f;
            var normY = (map.YCoord - 1f) * scaleFactor / 41f;
            var pixX  = (int)(normX * width);
            var pixY  = (int)(normY * height);

            log.Debug("[MapImageService] Pin at pixel ({PX},{PY}) on {W}×{H} | XCoord={XC:F1} YCoord={YC:F1} normX={NX:F4} normY={NY:F4}",
                pixX, pixY, width, height, map.XCoord, map.YCoord, normX, normY);

            // Scale pin radius so it stays proportional after downscaling
            var scaledRadius = PinRadius * (width / (float)OutputSize);

            img.Mutate(ctx =>
            {
                // White border ring
                ctx.Fill(Color.White,
                    new EllipsePolygon(pixX, pixY, scaledRadius + 3));

                // Red fill
                ctx.Fill(Color.FromRgb(220, 30, 30),
                    new EllipsePolygon(pixX, pixY, scaledRadius));

                ctx.Resize(OutputSize, OutputSize);
            });

            using var ms = new MemoryStream();
            img.Save(ms, new JpegEncoder { Quality = 85 });
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[MapImageService] Failed to generate pin image for {Zone}", map.PlaceName);
            return null;
        }
    }
}
