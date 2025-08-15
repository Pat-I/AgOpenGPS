﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using AgOpenGPS.Core;
using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Protocols.ISOBUS
{
    public static class IsoXmlParserHelpers
    {
        // Extract WGS84 origin from PLN or GGP lines
        public static bool TryExtractOrigin(XmlNodeList fieldParts, out Wgs84 origin)
        {
            double latSum = 0, lonSum = 0;
            int count = 0;

            foreach (XmlNode node in fieldParts)
            {
                if (node.Name == "PLN" && node.Attributes["A"]?.Value == "1")
                {
                    AccumulateCoordinates(node, ref latSum, ref lonSum, ref count);
                }
            }

            if (count == 0)
            {
                foreach (XmlNode node in fieldParts)
                {
                    if (node.Name == "GGP")
                    {
                        var lsg = node.SelectSingleNode("GPN/LSG");
                        if (lsg != null) AccumulateCoordinates(lsg.ParentNode, ref latSum, ref lonSum, ref count);
                    }
                }
            }

            if (count == 0)
            {
                origin = new Wgs84();
                return false;
            }

            origin = new Wgs84(latSum / count, lonSum / count);
            return true;
        }

        // Parse PLN boundaries into CBoundaryList objects
        public static List<CBoundaryList> ParseBoundaries(XmlNodeList fieldParts, ApplicationModel appModel)
        {
            List<CBoundaryList> boundaries = new List<CBoundaryList>();
            bool outerBuilt = false;

            foreach (XmlNode node in fieldParts)
            {
                if (node.Name != "PLN") continue;
                string type = node.Attributes["A"]?.Value;

                if ((type == "1" || type == "9") && !outerBuilt)
                {
                    if (node.SelectSingleNode("LSG[@A='1']") is XmlNode lsg)
                    {
                        boundaries.Add(ParseBoundaryFromLSG(lsg, appModel));
                        outerBuilt = true;
                    }
                }
                else if (type == "3" || type == "4" || type == "6")
                {
                    if (node.SelectSingleNode("LSG[@A='1']") is XmlNode lsg)
                    {
                        boundaries.Add(ParseBoundaryFromLSG(lsg, appModel));
                    }
                }
            }

            return boundaries;
        }
        // Parse Headland if available
        public static List<vec3> ParseHeadland(XmlNodeList fieldParts, ApplicationModel appModel)
        {
            foreach (XmlNode node in fieldParts)
            {
                if (node.Name == "PLN" && node.Attributes["A"]?.Value == "10")
                {
                    if (node.SelectSingleNode("LSG[@A='1']") is XmlNode lsg)
                    {
                        var list = new List<vec3>();
                        foreach (XmlNode pnt in lsg.SelectNodes("PNT"))
                        {
                            if (double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                                double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                            {
                                GeoCoord geo = appModel.LocalPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
                                list.Add(new vec3(geo));
                            }
                        }

                        CurveCABTools.CalculateHeadings(ref list);
                        return list;
                    }
                }
            }

            return new List<vec3>();
        }


        // Parse all valid guidance lines
        public static List<CTrk> ParseAllGuidanceLines(XmlNodeList fieldParts, ApplicationModel appModel)
        {
            List<CTrk> tracks = new List<CTrk>();

            foreach (XmlNode node in fieldParts)
            {
                if (node.Name == "GGP")
                {
                    var trk = ParseGGPNode(node, appModel);
                    if (trk != null) tracks.Add(trk);
                }
                else if (node.Name == "LSG" && node.Attributes["A"]?.Value == "5")
                {
                    var trk = ParseLSGNode(node, appModel);
                    if (trk != null) tracks.Add(trk);
                }
            }

            return tracks;
        }

        public static CBoundaryList ParseBoundaryFromLSG(XmlNode lsg, ApplicationModel appModel)
        {
            var list = new CBoundaryList();

            foreach (XmlNode pnt in lsg.SelectNodes("PNT"))
            {
                if (double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    GeoCoord geo = appModel.LocalPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
                    list.fenceLine.Add(new vec3(geo));
                }
            }

            return list;
        }

        // Parse GGP → GPN → LSG line
        public static CTrk ParseGGPNode(XmlNode node, ApplicationModel appModel)
        {
            var gpn = node.SelectSingleNode("GPN");
            var lsg = gpn?.SelectSingleNode("LSG[@A='5']");
            if (gpn == null || lsg == null) return null;

            string lineType = gpn.Attributes["C"]?.Value;
            string name = gpn.Attributes["B"]?.Value ?? node.Attributes["B"]?.Value ?? "Unnamed";

            if (lineType == "1") return ParseABLine(lsg, name, appModel);
            else if (lineType == "3") return ParseCurveLine(lsg, name, appModel);

            return null;
        }

        // Parse LSG line directly
        public static CTrk ParseLSGNode(XmlNode lsg, ApplicationModel appModel)
        {
            string name = lsg.Attributes["B"]?.Value ?? "Unnamed";
            int count = lsg.SelectNodes("PNT").Count;

            if (count == 2) return ParseABLine(lsg, name, appModel);
            else if (count > 2) return ParseCurveLine(lsg, name, appModel);

            return null;
        }

        // Parse AB line into CTrk
        private static CTrk ParseABLine(XmlNode lsg, string name, ApplicationModel appModel)
        {
            var points = lsg.SelectNodes("PNT");
            if (points.Count < 2) return null;

            var ptA = ParseVec2(points[0], appModel);
            var ptB = ParseVec2(points[1], appModel);

            double heading = Math.Atan2(ptB.easting - ptA.easting, ptB.northing - ptA.northing);
            if (heading < 0) heading += glm.twoPI;

            return new CTrk
            {
                heading = heading,
                mode = TrackMode.AB,
                ptA = ptA,
                ptB = ptB,
                name = name.Trim()
            };
        }

        // Parse Curve line into CTrk
        private static CTrk ParseCurveLine(XmlNode lsg, string name, ApplicationModel appModel)
        {
            var points = lsg.SelectNodes("PNT");
            if (points.Count <= 2) return null;

            var desList = new List<vec3>();
            foreach (XmlNode pnt in points)
            {
                var geo = ParseGeoCoord(pnt, appModel);
                desList.Add(new vec3(geo));
            }

            CurveCABTools.Preprocess(ref desList, 1.6, 0.5); // afgeleid van jouw CABCurve gedrag

            double avgHeading = CurveCABTools.ComputeAverageHeading(desList);

            var track = new CTrk
            {
                heading = avgHeading,
                mode = TrackMode.Curve,
                ptA = new vec2(desList[0].easting, desList[0].northing),
                ptB = new vec2(desList[desList.Count - 1].easting, desList[desList.Count - 1].northing),
                name = string.IsNullOrWhiteSpace(name) ? "Curve_" + DateTime.Now.ToString("HHmmss") : name
            };

            foreach (var pt in desList)
            {
                track.curvePts.Add(pt);
            }

            return track;
        }

        // Helpers
        private static void AccumulateCoordinates(XmlNode parent, ref double latSum, ref double lonSum, ref int count)
        {
            foreach (XmlNode pnt in parent.SelectNodes(".//PNT"))
            {
                if (double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    latSum += lat;
                    lonSum += lon;
                    count++;
                }
            }
        }

        private static vec2 ParseVec2(XmlNode pnt, ApplicationModel appModel)
        {
            double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat);
            double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon);
            GeoCoord geo = appModel.LocalPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
            return new vec2(geo);
        }

        private static GeoCoord ParseGeoCoord(XmlNode pnt, ApplicationModel appModel)
        {
            double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat);
            double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon);
            return appModel.LocalPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
        }
    }
}
