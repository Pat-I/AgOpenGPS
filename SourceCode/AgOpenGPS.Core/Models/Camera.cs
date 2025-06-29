﻿using AgOpenGPS.Core.DrawLib;

namespace AgOpenGPS.Core
{
    public class Camera
    {
        public Camera(double pitchInDegrees, double zoomValue)
        {
            PitchInDegrees = pitchInDegrees;
            ZoomValue = zoomValue;
            DistanceToLookAt = 0.5 * 75.0;
            FollowDirectionHint = true;
        }

        public double PitchInDegrees { get; set; } // 0.0 is vertical downwards -90.0 is horizontal
        public double ZoomValue { get; set; } // Beware: bigger values mean more zoomed out!

        public double DistanceToLookAt { get; set; }
        public double PanX { get; set; }
        public double PanY { get; set; }
        public bool FollowDirectionHint { get; set; }

        // Deprecated: Only here to avoid numerous changes to existing code.
        // Beware: the name suggests that this always has a positive value, but it is always negative!
        // Please use DistanceToLookAt instead
        public double camSetDistance => - 2.0 * DistanceToLookAt;

        public void SetLookAt(double lookAtX, double lookAtY, double directionHintInDegrees)
        {
            //back the camera up
            GLW.Translate(0,0, - DistanceToLookAt);

            GLW.RotateX(PitchInDegrees);
            GLW.Translate(PanX, PanY);

            if (FollowDirectionHint)
            {
                GLW.RotateZ(directionHintInDegrees);
            }
            GLW.Translate(-lookAtX, -lookAtY, 0.0);
        }
    }
}