using System.Collections.Generic;
using System.Globalization;
using BamboozClipStudio.Models;

namespace BamboozClipStudio.Services;

public class FilterService
{
    /// <summary>
    /// Builds the <c>-vf</c> argument value for FFmpeg from the given <see cref="FilterPreset"/>.
    /// Returns an empty string when all values are at their defaults (no filter needed).
    /// All floating-point numbers are formatted with invariant culture to prevent
    /// locale-dependent comma decimal separators.
    /// </summary>
    public string BuildVfChain(FilterPreset preset)
    {
        var components = new List<string>();

        // eq filter — emit whenever any of its parameters deviate from defaults.
        if (preset.Saturation != 1.0 ||
            preset.Brightness != 0.0 ||
            preset.Contrast != 1.0 ||
            preset.Gamma != 1.0)
        {
            string sat = preset.Saturation.ToString("G4", CultureInfo.InvariantCulture);
            string br  = preset.Brightness.ToString("G4", CultureInfo.InvariantCulture);
            string con = preset.Contrast.ToString("G4", CultureInfo.InvariantCulture);
            string gam = preset.Gamma.ToString("G4", CultureInfo.InvariantCulture);

            components.Add($"eq=saturation={sat}:brightness={br}:contrast={con}:gamma={gam}");
        }

        // unsharp filter.
        if (preset.Sharpness != 0.0)
        {
            string luma   = preset.Sharpness.ToString("G4", CultureInfo.InvariantCulture);
            string chroma = (preset.Sharpness / 2.0).ToString("G4", CultureInfo.InvariantCulture);
            components.Add($"unsharp=luma_amount={luma}:chroma_amount={chroma}");
        }

        return string.Join(",", components);
    }
}
