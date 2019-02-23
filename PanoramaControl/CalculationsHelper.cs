using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PanoramaControlApp
{
    public static class CalculationsHelper
    {
        //Calculate the Field of View
        public static string CalcFOV()
        {
            string OutputText = "";
            if (Params.FocalLength == 0)
            {
                OutputText="FocalLength=0 is not allowed\n";
                return OutputText;
            }

            if (Params.ChipWidth == 0)
            {
                OutputText = "ChipWidth=0 is not allowed\n";
                return OutputText;
            }

            if (Params.ChipHeight == 0)
            {
                OutputText = "ChipHeight=0 is not allowed\n";
                return OutputText;
            }

            //FOVx
            Params.FOVx = (double)(2 * (180 / Math.PI) * Math.Atan(Params.ChipWidth / (2 * Params.FocalLength)));

            //FOVy
            Params.FOVy = (double)(2 * (180 / Math.PI) * Math.Atan(Params.ChipHeight / (2 * Params.FocalLength)));

            Params.FOVx = Math.Round(Params.FOVx, 3);
            Params.FOVy = Math.Round(Params.FOVy, 3);

            return OutputText;
        }

    }
}
