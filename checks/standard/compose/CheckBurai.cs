﻿using MapsetParser.objects;
using MapsetParser.objects.hitobjects;
using MapsetParser.objects.timinglines;
using MapsetParser.statics;
using MapsetVerifier;
using MapsetVerifier.objects;
using MapsetVerifier.objects.metadata;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace MapsetChecks.checks.timing
{
    public class CheckBurai : BeatmapCheck
    {
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata()
        {
            Modes = new Beatmap.Mode[]
            {
                Beatmap.Mode.Standard
            },
            Category = "Compose",
            Message = "Burai slider.",
            Author = "Naxess"
        };
        
        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>()
            {
                { "Unrankable",
                    new IssueTemplate(Issue.Level.Unrankable,
                        "{0} Burai.",
                        "timestamp - ")
                    .WithCause(
                        "The burai score of a slider shape, based on the distance and delta angle between intersecting parts of " +
                        "the curve, is very high.") },

                { "Warning",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Potentially burai.",
                        "timestamp - ")
                    .WithCause(
                        "Same as the other check, but with a lower score threshold.") }
            };
        }
        
        public override IEnumerable<Issue> GetIssues(Beatmap aBeatmap)
        {
            foreach (HitObject hitObject in aBeatmap.hitObjects)
            {
                if (hitObject is Slider slider && slider.curveType == Slider.CurveType.Bezier)
                {
                    // Make sure the path doesn't go back on itself (basically the angle shouldn't be too similar
                    // between intersecting parts of a slider). Only checks sections of a slider that have some
                    // distance in time between each other, allowing very small burai-like structure, which is
                    // usually readable.
                    
                    double angleIntersect;
                    double otherAngleIntersect;
                    double diffAngleIntersect;

                    bool passedMargin = false;
                    
                    double distance;
                    double maxDistance = 3;
                    
                    List<double> buraiScores = new List<double>();

                    for (int i = 1; i < slider.pathPxPositions.Count; ++i)
                    {
                        passedMargin = false;

                        // Only check places we haven't been yet for optimization.
                        for (int j = i + 1; j < slider.pathPxPositions.Count - 1; ++j)
                        {
                            distance = GetDistance(slider.pathPxPositions[i], slider.pathPxPositions[j]);
                            
                            // First ensure the point is far enough away to not be a small burai structure.
                            if (!passedMargin && distance >= maxDistance)
                                passedMargin = true;

                            // Then if it returns, we know the slider is intersecting itself.
                            if (passedMargin && distance < maxDistance)
                            {
                                angleIntersect = GetAngle(slider.pathPxPositions[i - 1], slider.pathPxPositions[i]);
                                otherAngleIntersect = GetAngle(slider.pathPxPositions[j], slider.pathPxPositions[j + 1]);
                                
                                // Compare the intersection angles, resets after 180 degrees since we're comparing tangents.
                                diffAngleIntersect = Math.Abs(WrapAngle(angleIntersect - otherAngleIntersect, 0.5));
                                
                                double distanceScore = 100 * Math.Sqrt(10) / Math.Pow(10, 2 * distance) / 125;
                                double angleScore = 1 / (Math.Pow(diffAngleIntersect / Math.PI * 20, 3) + 0.01) / 250;

                                buraiScores.Add(angleScore * distanceScore);
                            }
                        }
                    }

                    double totalBuraiScore = GetWeighedScore(buraiScores);
                    if (totalBuraiScore > 0)
                    {
                        // Note that this may false positive in places with slight but readable overlapping curves.
                        if (totalBuraiScore > 5)
                            yield return new Issue(GetTemplate("Unrankable"),
                                aBeatmap, Timestamp.Get(hitObject));
                        else if (totalBuraiScore > 1)
                            yield return new Issue(GetTemplate("Warning"),
                                aBeatmap, Timestamp.Get(hitObject), Issue.Level.Warning);
                    }
                }
            }
        }

        /// <summary> Returns the smallest angle in radians. </summary>
        private double WrapAngle(double aRadians, double aScale = 1)
        {
            return aRadians > Math.PI * aScale ? Math.PI * 2 * aScale - aRadians : aRadians;
        }

        /// <summary> Returns the angle between two 2D vectors, a value between 0 and 2 PI. </summary>
        private double GetAngle(Vector2 aVector, Vector2 anOtherVector, double aWrapScale = 1)
        {
            double radians =
                WrapAngle(
                    Math.Atan2(
                        aVector.Y - anOtherVector.Y,
                        aVector.X - anOtherVector.X),
                    aWrapScale);
            
            return (radians >= 0 ? radians : Math.PI * 2 + radians) % Math.PI;
        }

        /// <summary> Returns the euclidean distance between two 2D vectors. </summary>
        private double GetDistance(Vector2 aVector, Vector2 anOtherVector)
        {
            return
                Math.Sqrt(
                    Math.Pow(aVector.X - anOtherVector.X, 2) +
                    Math.Pow(aVector.Y - anOtherVector.Y, 2));
        }

        /// <summary> Returns the weighted score of burai scores, decaying by 90% for each lower number. </summary>
        private double GetWeighedScore(List<double> aBuraiScores)
        {
            double score = 0;

            // Sort by highest impact and then each following is worth less.
            List<double> sortedScores = aBuraiScores.OrderByDescending(aNumber => aNumber).ToList();
            for (int i = 0; i < sortedScores.Count; ++i)
                score += sortedScores[i] * Math.Pow(0.9, i);

            return score;
        }
    }
}
