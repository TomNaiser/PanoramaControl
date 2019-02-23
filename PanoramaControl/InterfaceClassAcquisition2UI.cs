using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PanoramaControlApp
{
    class InterfaceClassAcquisition2UI
    {
        public double ServoXPos { get; set; }
        public double ServoYPos { get; set; }
        public string CommentText { get; set; }
        public bool UpdateServoPos { get; set; }
        public bool UpdateCommentText { get; set; }
    }
}
