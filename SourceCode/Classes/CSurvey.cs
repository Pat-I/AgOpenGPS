using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CSurveyPt
    {
        public double altitude { get; set; }
        public double easting { get; set; }
        public double northing { get; set; }
        public double heading { get; set; }
        public double cutAltitude { get; set; }
        public double lastPassAltitude { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public double distance { get; set; }
        public bool hasCut = false;
        public bool isclose = false;

        public int index = 0;
        public string comment;

        //constructor
        public CSurveyPt(double _easting, double _heading, double _northing,
                            double _altitude, double _lat, double _long,
                            double _cutAltitude = -1, double _lastPassAltitude = -1, double _distance = -1)
        {
            easting = _easting;
            northing = _northing;
            heading = _heading;
            altitude = _altitude- Properties.Vehicle.Default.setVehicle_antennaHeight;
            latitude = _lat;
            longitude = _long;

            //optional parameters
            cutAltitude = _cutAltitude;
            lastPassAltitude = _lastPassAltitude;
            distance = _distance;
            hasCut = false;
            isclose = false;
            comment = "3GRD";

            index = 0;

        }
    }

    public class CSurvey
    {
        //copy of the mainform address
        //constructor
       

        //pointers to mainform controls
        private readonly FormGPS mf;

        //private readonly OpenGL gl;

        public bool isSurveyOn = false;
        public bool isSurveyBtnOn, isShowCut;
        public int click=0;
        public double slope = 0.002;
        public double zeroAltitude = 0;
        public double avgGrade = 0;
        public double cosSectionHeading = 1.0, sinSectionHeading = 0.0;
        public double cutValue = 0;
        

        public List<CSurveyPt> ptList = new List<CSurveyPt>();
        public List<CSurveyPt> ABptList = new List<CSurveyPt>();

        //used to determine if section was off and now is on or vice versa
        public bool wasSectionOn;

        //generated box for finding closest point
        public vec2 boxA = new vec2(0, 0), boxB = new vec2(0, 2);

        public vec2 boxC = new vec2(1, 1), boxD = new vec2(2, 3);

        //angle to path line closest point and fix
        public double refHeading, ref2;

        // for closest line point to current fix
        public double minDistance = 99999.0, refX, refZ;

        //generated reference line
        public double refLineSide = 1.0;

        public vec2 refPoint1 = new vec2(1, 1), refPoint2 = new vec2(2, 2);

        public double distanceFromRefLine;
        public double distanceFromCurrentLine;

        private int A, B, C;
        public double abFixHeadingDelta, abHeading;

        public bool isABSameAsFixHeading = true;
        public bool isOnRightSideCurrentLine = true;

        public bool isDrawingRefLine;

        //pure pursuit values
        public vec2 goalPointCT = new vec2(0, 0);

        public vec2 radiusPointCT = new vec2(0, 0);
        public double steerAngleCT;
        public double rEastCT, rNorthCT;
        public double ppRadiusCT;

        //list of Survey data from GPS
        //public List<vec4> ptList = new List<vec4>();

        //the manually picked list
        public List<vec2> drawList = new List<vec2>();

        //converted from drawn line to all points cut line
        //public List<vec4> cutList = new List<vec4>();

        //list of the list of individual Lines for entire field
        //public List<CSurveyPt> topoList = new List<CSurveyPt>();

        //constructor
        public CSurvey(FormGPS _f)
        {
            mf = _f;
            //if (mf != null)
            //{
            //    gl = _gl;
            //}
        }

        //start stop and add points to list
        public void StartSurveyLine()
        {
            isSurveyOn = true;
            //reuse ptList
            ptList.Clear();

            CSurveyPt point = new CSurveyPt(mf.pivotAxlePos.easting, mf.fixHeading, mf.pivotAxlePos.northing, mf.pn.altitude, mf.pn.latitude, mf.pn.longitude);
            ptList.Add(point);
            ptList[ptList.Count - 1].index = ptList.Count;
        }

        //Add current position to ptList
        public void AddPoint()
        {
            CSurveyPt point = new CSurveyPt(mf.pivotAxlePos.easting, mf.fixHeading, mf.pivotAxlePos.northing, mf.pn.altitude, mf.pn.latitude, mf.pn.longitude);
            ptList.Add(point);
            ptList[ptList.Count - 1].index = ptList.Count;
        }

        //End the strip
        public void StopSurveyLine()
        {
            CSurveyPt point = new CSurveyPt(mf.pivotAxlePos.easting, mf.fixHeading, mf.pivotAxlePos.northing, mf.pn.altitude, mf.pn.latitude, mf.pn.longitude);
            ptList.Add(point);
            ptList[ptList.Count - 1].index = ptList.Count;

            //turn it off
            isSurveyOn = false;
        }

        //determine distance from Survey guidance line
        //public void DistanceFromSurveyLine()
        //{
        //    double minDistA = 1000000, minDistB = 1000000;
        //    int ptCount = ptList.Count;
        //    //distanceFromCurrentLine = 9999;
        //    if (ptCount > 0)
        //    {
        //        //find the closest 2 points to current fix
        //        for (int t = 0; t < ptCount; t++)
        //        {
        //            double dist = ((mf.pivotAxlePos.easting - ptList[t].easting) * (mf.pivotAxlePos.easting - ptList[t].easting))
        //                            + ((mf.pivotAxlePos.northing - ptList[t].northing) * (mf.pivotAxlePos.northing - ptList[t].northing));
        //            if (dist < minDistA)
        //            {
        //                minDistB = minDistA;
        //                B = A;
        //                minDistA = dist;
        //                A = t;
        //            }
        //            else if (dist < minDistB)
        //            {
        //                minDistB = dist;
        //                B = t;
        //            }
        //        }

        //        //just need to make sure the points continue ascending or heading switches all over the place
        //        if (A > B) { C = A; A = B; B = C; }

        //        //get the distance from currently active AB line
        //        //x2-x1
        //        double dx = ptList[B].easting - ptList[A].easting;
        //        //z2-z1
        //        double dz = ptList[B].northing - ptList[A].northing;

        //        if (Math.Abs(dx) < Double.Epsilon && Math.Abs(dz) < Double.Epsilon) return;

        //        //abHeading = Math.Atan2(dz, dx);
        //        abHeading = ptList[A].heading;

        //        //how far from current AB Line is fix
        //        distanceFromCurrentLine = ((dz * mf.pivotAxlePos.easting) - (dx * mf.pivotAxlePos.northing)
        //            + (ptList[B].easting * ptList[A].northing) - (ptList[B].northing * ptList[A].easting))
        //                        / Math.Sqrt((dz * dz) + (dx * dx));

        //        //are we on the right side or not
        //        isOnRightSideCurrentLine = distanceFromCurrentLine > 0;

        //        //absolute the distance
        //        distanceFromCurrentLine = Math.Abs(distanceFromCurrentLine);

        //        // ** Pure pursuit ** - calc point on ABLine closest to current position
        //        double U = (((mf.pivotAxlePos.easting - ptList[A].easting) * (dx))
        //                    + ((mf.pivotAxlePos.northing - ptList[A].northing) * (dz)))
        //                    / ((dx * dx) + (dz * dz));

        //        rEastCT = ptList[A].easting + (U * (dx));
        //        rNorthCT = ptList[A].northing + (U * (dz));

        //        //Subtract the two headings, if > 1.57 its going the opposite heading as refAB
        //        abFixHeadingDelta = (Math.Abs(mf.fixHeading - abHeading));
        //        if (abFixHeadingDelta >= Math.PI) abFixHeadingDelta = Math.Abs(abFixHeadingDelta - glm.twoPI);

        //        //used for accumulating distance to find goal point
        //        double distSoFar;

        //        //how far should goal point be away  - speed * seconds * kmph -> m/s + min value
        //        double goalPointDistance = mf.pn.speed * mf.vehicle.goalPointLookAhead * 0.27777777;

        //        //minimum of 4.0 meters look ahead
        //        if (goalPointDistance < 3.0) goalPointDistance = 3.0;

        //        // used for calculating the length squared of next segment.
        //        double tempDist = 0.0;

        //        if (abFixHeadingDelta >= glm.PIBy2)
        //        {
        //            //counting down
        //            isABSameAsFixHeading = false;
        //            distSoFar = mf.pn.Distance(ptList[A].northing, ptList[A].easting, rNorthCT, rEastCT);
        //            //Is this segment long enough to contain the full lookahead distance?
        //            if (distSoFar > goalPointDistance)
        //            {
        //                //treat current segment like an AB Line
        //                goalPointCT.easting = rEastCT - (Math.Sin(ptList[A].heading) * goalPointDistance);
        //                goalPointCT.northing = rNorthCT - (Math.Cos(ptList[A].heading) * goalPointDistance);
        //            }

        //            //multiple segments required
        //            else
        //            {
        //                //cycle thru segments and keep adding lengths. check if start and break if so.
        //                while (A > 0)
        //                {
        //                    B--; A--;
        //                    tempDist = mf.pn.Distance(ptList[B].northing, ptList[B].easting, ptList[A].northing, ptList[A].easting);

        //                    //will we go too far?
        //                    if ((tempDist + distSoFar) > goalPointDistance)
        //                    {
        //                        //A++; B++;
        //                        break; //tempDist contains the full length of next segment
        //                    }
        //                    else
        //                    {
        //                        distSoFar += tempDist;
        //                    }
        //                }

        //                double t = (goalPointDistance - distSoFar); // the remainder to yet travel
        //                t /= tempDist;

        //                goalPointCT.easting = (((1 - t) * ptList[B].easting) + (t * ptList[A].easting));
        //                goalPointCT.northing = (((1 - t) * ptList[B].northing) + (t * ptList[A].northing));
        //            }
        //        }
        //        else
        //        {
        //            //counting up
        //            isABSameAsFixHeading = true;
        //            distSoFar = mf.pn.Distance(ptList[B].northing, ptList[B].easting, rNorthCT, rEastCT);

        //            //Is this segment long enough to contain the full lookahead distance?
        //            if (distSoFar > goalPointDistance)
        //            {
        //                //treat current segment like an AB Line
        //                goalPointCT.easting = rEastCT + (Math.Sin(ptList[A].heading) * goalPointDistance);
        //                goalPointCT.northing = rNorthCT + (Math.Cos(ptList[A].heading) * goalPointDistance);
        //            }

        //            //multiple segments required
        //            else
        //            {
        //                //cycle thru segments and keep adding lengths. check if end and break if so.
        //                // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
        //                while (B < ptCount - 1)
        //                {
        //                    B++; A++;
        //                    tempDist = mf.pn.Distance(ptList[B].northing, ptList[B].easting, ptList[A].northing, ptList[A].easting);

        //                    //will we go too far?
        //                    if ((tempDist + distSoFar) > goalPointDistance)
        //                    {
        //                        //A--; B--;
        //                        break; //tempDist contains the full length of next segment
        //                    }

        //                    distSoFar += tempDist;
        //                }

        //                //xt = (((1 - t) * x0 + t * x1)
        //                //yt = ((1 - t) * y0 + t * y1))

        //                double t = (goalPointDistance - distSoFar); // the remainder to yet travel
        //                t /= tempDist;

        //                goalPointCT.easting = (((1 - t) * ptList[A].easting) + (t * ptList[B].easting));
        //                goalPointCT.northing = (((1 - t) * ptList[A].northing) + (t * ptList[B].northing));
        //            }
        //        }

        //        //calc "D" the distance from pivot axle to lookahead point
        //        double goalPointDistanceSquared = mf.pn.DistanceSquared(goalPointCT.northing, goalPointCT.easting, mf.pn.northing, mf.pn.easting);

        //        //calculate the the delta x in local coordinates and steering angle degrees based on wheelbase
        //        double localHeading = glm.twoPI - mf.fixHeading;
        //        ppRadiusCT = goalPointDistanceSquared / (2 * (((goalPointCT.easting - mf.pivotAxlePos.easting) * Math.Cos(localHeading)) + ((goalPointCT.northing - mf.pn.northing) * Math.Sin(localHeading))));

        //        steerAngleCT = glm.toDegrees(Math.Atan(2 * (((goalPointCT.easting - mf.pivotAxlePos.easting) * Math.Cos(localHeading))
        //            + ((goalPointCT.northing - mf.pivotAxlePos.northing) * Math.Sin(localHeading))) * mf.vehicle.wheelbase / goalPointDistanceSquared));

        //        if (steerAngleCT < -mf.vehicle.maxSteerAngle) steerAngleCT = -mf.vehicle.maxSteerAngle;
        //        if (steerAngleCT > mf.vehicle.maxSteerAngle) steerAngleCT = mf.vehicle.maxSteerAngle;

        //        if (ppRadiusCT < -500) ppRadiusCT = -500;
        //        if (ppRadiusCT > 500) ppRadiusCT = 500;

        //        radiusPointCT.easting = mf.pivotAxlePos.easting + (ppRadiusCT * Math.Cos(localHeading));
        //        radiusPointCT.northing = mf.pivotAxlePos.northing + (ppRadiusCT * Math.Sin(localHeading));

        //        //angular velocity in rads/sec  = 2PI * m/sec * radians/meters
        //        double angVel = glm.twoPI * 0.277777 * mf.pn.speed * (Math.Tan(glm.toRadians(steerAngleCT))) / mf.vehicle.wheelbase;

        //        //clamp the steering angle to not exceed safe angular velocity
        //        if (Math.Abs(angVel) > mf.vehicle.maxAngularVelocity)
        //        {
        //            steerAngleCT = glm.toDegrees(steerAngleCT > 0 ?
        //                    (Math.Atan((mf.vehicle.wheelbase * mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.pn.speed * 0.277777)))
        //                : (Math.Atan((mf.vehicle.wheelbase * -mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.pn.speed * 0.277777))));
        //        }
        //        //Convert to centimeters
        //        distanceFromCurrentLine = Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);

        //        //distance is negative if on left, positive if on right
        //        //if you're going the opposite direction left is right and right is left
        //        //double temp;
        //        if (isABSameAsFixHeading)
        //        {
        //            //temp = (abHeading);
        //            if (!isOnRightSideCurrentLine) distanceFromCurrentLine *= -1.0;
        //        }

        //        //opposite way so right is left
        //        else
        //        {
        //            //temp = (abHeading - Math.PI);
        //            //if (temp < 0) temp = (temp + glm.twoPI);
        //            //temp = glm.toDegrees(temp);
        //            if (isOnRightSideCurrentLine) distanceFromCurrentLine *= -1.0;
        //        }

        //        mf.guidanceLineDistanceOff = (Int16)distanceFromCurrentLine;
        //        mf.guidanceLineSteerAngle = (Int16)(steerAngleCT * 10);
        //        //mf.guidanceLineHeadingDelta = (Int16)((Math.Atan2(Math.Sin(temp - mf.fixHeading),
        //        //                                    Math.Cos(temp - mf.fixHeading))) * 10000);
        //    }
        //    else
        //    {
        //        //invalid distance so tell AS module
        //        distanceFromCurrentLine = 32000;
        //        mf.guidanceLineDistanceOff = 32000;
        //    }
        //}

        public int GetSurveyPt(double _easting, double _northing, List<CSurveyPt> _ptList)
        {
            int alfa = 0;
            int ptCount = _ptList.Count - 1;
            for (int t = 0; t < ptCount; t++)
            {
                
                if (_ptList[t].easting == _easting&& _ptList[t].northing==_northing)
                {
                        alfa = t;
                        return alfa;
                }
                
            }
            return alfa;
                
        }

        //draw the red follow me line
        public void DrawSurveyLine()
        {
            //GL.Color(0.98f, 0.98f, 0.50f);
            //GL.Begin(OpenGL.GL_LINE_STRIP);
            ////for (int h = 0; h < ptCount; h++) GL.Vertex(guideList[h].x, 0, guideList[h].z);
            //GL.Vertex(boxA.easting, boxA.northing, 0);
            //GL.Vertex(boxB.easting, boxB.northing, 0);
            //GL.Vertex(boxC.easting, boxC.northing, 0);
            //GL.Vertex(boxD.easting, boxD.northing, 0);
            //GL.Vertex(boxA.easting, boxA.northing, 0);
            //GL.End();

            ////draw the guidance line
            int ptCount = ptList.Count;
            //GL.LineWidth(2);
            //GL.Color(0.98f, .9f, 0.0f);
            //GL.Begin(OpenGL.GL_LINE_STRIP);
            //for (int h = 0; h < ptCount; h++)
            //{

            //    GL.Vertex(ptList[h].easting, ptList[h].northing, 0);
            //}
            //GL.End();

            //GL.PointSize(4.0f);
            //GL.Begin(OpenGL.GL_POINTS);

            ////GL.Color(0.97f, .9f, 0.45f);
            //for (int h = 0; h < ptCount; h++)
            //{
            //    float green = Convert.ToSinGLe( Math.Abs(255*((ptList[h].altitude-100)/100)/255));
            //    float red = Convert.ToSinGLe(256/ 256);
            //    float blue = Convert.ToSinGLe(0 / 256);
            //    GL.Color(red, green, blue);
            //    GL.Vertex(ptList[h].easting, ptList[h].northing, 0);
            //}

            //GL.End();
           

            //draw the reference line
            
            GL.LineWidth(1);
            if (!isSurveyBtnOn)
            {

                ptCount = ptList.Count;
                if (ptCount > 0)
                {


                    double toole = Properties.Vehicle.Default.setVehicle_toolWidth;
                    double avg = 0;
                    int avgCount = 0;
                    for (int k = 0; k < ptCount; k++)
                    {
                        if (ptList[k].altitude > 0)
                        {
                            avg += ptList[k].altitude;
                            avgCount += 1;
                        }
                    }
                    avg = avg / avgCount;

                    double max = avg;
                    double min = avg;
                    double range = 0;
                    for (int k = 0; k < ptCount; k++)
                    {
                        if (ptList[k].altitude != -1)
                        {
                            if (ptList[k].altitude >= max) max = ptList[k].altitude;
                            if (ptList[k].altitude <= min) min = ptList[k].altitude;
                            range = (max - min) / 2;
                        }
                    }

                    avgGrade = avg;


                    for (int i = 1; i < ptCount; i++)
                    {
                        if (ptList[i].altitude != -1)
                        {
                            float green = Convert.ToSingle(256 / 256);
                            float red = Convert.ToSingle(256 / 256);
                            float blue = Convert.ToSingle(256 / 256);
                            sinSectionHeading = Math.Sin(-ptList[i].heading);
                            cosSectionHeading = Math.Cos(-ptList[i].heading);
                            if (avg <= ptList[i].altitude)
                            {
                                green = Convert.ToSingle(1 - ((ptList[i].altitude - avg) / (max - avg)));
                                blue = Convert.ToSingle(1 - ((ptList[i].altitude - avg) / (max - avg)));
                            }
                            if (ptList[i].altitude < avg)
                            {
                                red = Convert.ToSingle(1 - ((avg - ptList[i].altitude) / (avg - min)));
                                blue = Convert.ToSingle(1 - ((avg - ptList[i].altitude) / (avg - min)));
                            }

                            GL.Color3(red, green, blue);
                            GL.Begin(PrimitiveType.Lines);
                            GL.Vertex3((cosSectionHeading * -toole / 2) + ptList[i].easting,
                               (sinSectionHeading * -toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                            GL.Vertex3((cosSectionHeading * toole / 2) + ptList[i].easting,
                               (sinSectionHeading * toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                            GL.Vertex3((sinSectionHeading * -toole / 2) + ptList[i].easting,
                           (cosSectionHeading * -toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                            GL.Vertex3((sinSectionHeading * toole / 2) + ptList[i].easting,
                               (cosSectionHeading * toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                            if (ptList[i].isclose)
                            {
                                red = 1;
                                blue = 0;
                                green = 1;
                                GL.Color3(red, green, blue);
                                GL.Vertex3(ptList[i].easting, ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3(ptList[i].easting, ptList[i].northing, ptList[i].altitude - avg + toole);
                            }




                            //GL.Vertex((cosSectionHeading * toole / 2) + ptList[i-1].easting,
                            //       (sinSectionHeading * toole / 2) + ptList[i-1].northing, ptList[i-1].altitude - avg);
                            //GL.Vertex((cosSectionHeading * -toole / 2) + ptList[i - 1].easting,
                            //   (sinSectionHeading * -toole / 2) + ptList[i - 1].northing, ptList[i - 1].altitude - avg);


                            GL.End();
                        }

                    }
                    //GL.LineWidth(1);
                    //GL.Color3(0.2f, .2f, 0.0f);
                    //GL.Begin(PrimitiveType.LineStrip);
                    //for (int h = 0; h < ptCount; h++)
                    //{

                    //    GL.Vertex3(ptList[h].easting, ptList[h].northing, ptList[h].altitude - avg);
                    //}
                    //GL.End();

                    //GL.LineWidth(1);
                    //GL.Color3(0.9f, .0f, 0.0f);
                    //GL.Begin(PrimitiveType.LineStrip);
                    //for (int h = 0; h < ABptList.Count; h++)
                    //{

                    //    GL.Vertex3(ABptList[h].easting, ABptList[h].northing, ABptList[h].altitude - avg);
                    //}
                    //GL.End();





                }


            }
            else
            {
                ptCount = ptList.Count;
                if (ptCount > 0)
                {


                    double toole = Properties.Vehicle.Default.setVehicle_toolWidth;
                    double avg = 0;
                    int avgCount = 0;
                    for (int k = 0; k < ptCount; k++)
                    {
                        if (ptList[k].cutAltitude != -1 && ptList[k].cutAltitude != 0)
                        {
                            avg += ptList[k].cutAltitude;
                            avgCount += 1;
                        }
                    }
                    avg = avg / avgCount;

                    double maxCut = 0;
                    double MaxFill = 0;

                    for (int k = 0; k < ptCount; k++)
                    {
                        if (ptList[k].cutAltitude != -1 && ptList[k].altitude != -1 && ptList[k].cutAltitude != 0)
                        {
                            double currentCut = ptList[k].altitude - ptList[k].cutAltitude;
                            double currentFill = ptList[k].cutAltitude - ptList[k].altitude;
                            if (currentCut >= maxCut) maxCut = currentCut;
                            else if (currentFill >= MaxFill) MaxFill = currentFill;

                        }

                    }



                    for (int i = 0; i < ptCount; i++)
                    {
                        double currentCut = ptList[i].altitude - ptList[i].cutAltitude;
                        double currentFill = ptList[i].cutAltitude - ptList[i].altitude;
                        float green = Convert.ToSingle(256 / 256);
                        float red = Convert.ToSingle(256 / 256);
                        float blue = Convert.ToSingle(256 / 256);
                        sinSectionHeading = Math.Sin(-ptList[i].heading);
                        cosSectionHeading = Math.Cos(-ptList[i].heading);
                        if (ptList[i].cutAltitude != -1 && ptList[i].altitude != -1 && ptList[i].cutAltitude != 0)
                        {
                            if (ptList[i].cutAltitude <= ptList[i].altitude && isShowCut)
                            {
                                green = Convert.ToSingle(1 - ((currentCut) / (maxCut)));
                                blue = Convert.ToSingle(1 - ((currentCut) / (maxCut)));
                                GL.Color3(red, green, blue);
                                GL.Begin(PrimitiveType.LineStrip);
                                GL.Vertex3((cosSectionHeading * -toole / 2) + ptList[i].easting,
                                   (sinSectionHeading * -toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3((cosSectionHeading * toole / 2) + ptList[i].easting,
                                   (sinSectionHeading * toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3((sinSectionHeading * -toole / 2) + ptList[i].easting,
                                   (cosSectionHeading * -toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3((sinSectionHeading * toole / 2) + ptList[i].easting,
                                   (cosSectionHeading * toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3(ptList[i].easting, ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3(ptList[i].easting, ptList[i].northing, ptList[i].cutAltitude - avg);

                                GL.End();
                            }
                            if (ptList[i].cutAltitude > ptList[i].altitude && !isShowCut)
                            {
                                red = Convert.ToSingle(1 - ((currentFill) / (MaxFill)));
                                blue = Convert.ToSingle(1 - ((currentFill) / (MaxFill)));
                                GL.Color3(red, green, blue);
                                GL.Begin(PrimitiveType.Lines);
                                GL.Vertex3((cosSectionHeading * -toole / 2) + ptList[i].easting,
                                   (sinSectionHeading * -toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3((cosSectionHeading * toole / 2) + ptList[i].easting,
                                   (sinSectionHeading * toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3((sinSectionHeading * -toole / 2) + ptList[i].easting,
                                   (cosSectionHeading * -toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3((sinSectionHeading * toole / 2) + ptList[i].easting,
                                   (cosSectionHeading * toole / 2) + ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3(ptList[i].easting, ptList[i].northing, ptList[i].altitude - avg);
                                GL.Vertex3(ptList[i].easting, ptList[i].northing, ptList[i].cutAltitude - avg);
                                GL.End();
                            }
                        }



                    }

                }
            }

            //ptCount = conList.Count;
            //if (ptCount > 0)
            //{
            ////draw closest point and side of line points
            //GL.Color(0.5f, 0.900f, 0.90f);
            //GL.PointSize(4.0f);
            //GL.Begin(OpenGL.GL_POINTS);
            //for (int i = 0; i < ptCount; i++)  GL.Vertex(conList[i].x, conList[i].z, 0);
            //GL.End();

            //GL.Color(0.35f, 0.30f, 0.90f);
            //GL.PointSize(6.0f);
            //GL.Begin(OpenGL.GL_POINTS);
            //GL.Vertex(conList[closestRefPoint].x, conList[closestRefPoint].z, 0);
            //GL.End();
            //}
            //if (mf.isPureDisplayOn)
            //{
            //    const int numSegments = 100;
            //    {
            //        GL.Color3(0.95f, 0.30f, 0.950f);

            //        double theta = glm.twoPI / (numSegments);
            //        double c = Math.Cos(theta);//precalculate the sine and cosine
            //        double s = Math.Sin(theta);

            //        double x = ppRadiusCT;//we start at anGLe = 0
            //        double y = 0;

            //        GL.LineWidth(1);
            //        GL.Begin(PrimitiveType.LineLoop);
            //        for (int ii = 0; ii < numSegments; ii++)
            //        {
            //            //GLVertex2f(x + cx, y + cy);//output vertex
            //            GL.Vertex3(x + radiusPointCT.easting, y + radiusPointCT.northing);//output vertex

            //            //apply the rotation matrix
            //            double t = x;
            //            x = (c * x) - (s * y);
            //            y = (s * t) + (c * y);
            //        }
            //        GL.End();

            //        //Draw lookahead Point
            //        GL.PointSize(4.0f);
            //        GL.Begin(OpenGL.GL_POINTS);

            //        //GL.Color(1.0f, 1.0f, 0.25f);
            //        //GL.Vertex(rEast, rNorth, 0.0);

            //        GL.Color(1.0f, 0.5f, 0.95f);
            //        GL.Vertex(goalPointCT.easting, goalPointCT.northing, 0.0);

            //        GL.End();
            //        GL.PointSize(1.0f);
            //    }
            //}
        }

        //Reset the Survey to zip
        public void ResetSurvey()
        {
            if (ptList != null) ptList.Clear();
        }
    }//class
}//namespace