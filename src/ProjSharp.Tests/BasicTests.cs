﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjSharp.Tests
{
    [TestClass]
    public class BasicTests
    {
        public TestContext TestContext { get; set; }
        [TestMethod]
        public void BadInit()
        {
            using (var pc = new ProjContext())
            {
                try
                {
                    using (var crs = CoordinateReferenceSystem.Create("!1@2#3$4%5^6&7*8(9)0", pc))
                    {
                    }
                    Assert.Fail();
                }
                catch (ProjException pe)
                {
                    Assert.AreEqual("proj_create: unrecognized format / unknown name", pe.GetBaseException().Message);
                }
            }
        }

        [TestMethod]
        public void EpsgVersionTest()
        {
            using (var pc = new ProjContext())
            {
                Assert.IsTrue(Proj.Version >= new Version(7, 2, 1));
                Assert.IsTrue(pc.EpsgVersion >= new Version(10, 0));
            }
        }

        [TestMethod]
        public void CreateAndDestroyContext()
        {
            using (var pc = new ProjContext())
            {
                using (var crs = pc.Create("+proj=merc +ellps=clrk66 +lat_ts=33"))
                {
                    Assert.AreEqual("PROJ-based coordinate operation", crs.Description);

                    if (crs is CoordinateOperation cob)
                    {
                        Assert.AreEqual(false, cob.DegreeInput);
                        Assert.AreEqual(false, cob.DegreeOutput);
                        Assert.AreEqual(true, cob.AngularInput);
                        Assert.AreEqual(false, cob.AngularOutput);

                        Assert.IsNull(crs.Identifiers);

                        var src = cob.GetSourceCoordinateReferenceSystem();
                        var dst = cob.GetTargetCoordinateReferenceSystem();

                        Assert.IsNull(src);
                        Assert.IsNull(dst);

                        //Assert.AreEqual(1, crs.Identifiers.Count);

                    }
                    else
                        Assert.Fail();

                    Assert.AreEqual(ProjType.OtherCoordinateOperation, crs.Type);
                    string expected =
@"{
  ""$schema"": ""https://proj.org/schemas/v0.2/projjson.schema.json"",
  ""type"": ""Conversion"",
  ""name"": ""PROJ-based coordinate operation"",
  ""method"": {
    ""name"": ""PROJ-based operation method: +proj=merc +ellps=clrk66 +lat_ts=33""
  }
}".Replace("\r", "");
                    Assert.AreEqual(expected, crs.AsProjJson());
                    Assert.AreEqual("proj=merc ellps=clrk66 lat_ts=33", crs.Definition);
                    Assert.AreEqual("+proj=merc +ellps=clrk66 +lat_ts=33", crs.AsProjString());
                }

                using (var crs = pc.Create(new string[] { "proj=merc", "ellps=clrk66", "lat_ts=33" }))
                {
                    Assert.AreEqual("PROJ-based coordinate operation", crs.Description);
                }
            }
        }


        [TestMethod]
        public void CreateAndDestroyContextEPSG()
        {
            using (var pc = new ProjContext())
            {
                GC.KeepAlive(pc.Clone());

                // Needs proj.db

                using (var crs = CoordinateReferenceSystem.Create("EPSG:25832", pc))
                {
                    Assert.AreEqual("ETRS89 / UTM zone 32N", crs.Description);

                    Assert.IsNotNull(crs.Identifiers);
                    Assert.AreEqual(1, crs.Identifiers.Count);
                    Assert.AreEqual("EPSG", crs.Identifiers[0].Authority);
                    Assert.AreEqual("25832", crs.Identifiers[0].Name);
                    Assert.AreEqual("+proj=utm +zone=32 +ellps=GRS80 +units=m +no_defs +type=crs", crs.AsProjString());

                    using (var t = ProjObject.Create(crs.AsProjString()))
                    {
                        Assert.IsTrue(t is CoordinateReferenceSystem);

                        Assert.IsNull(t.Identifiers);

                        Assert.AreEqual(crs.AsProjString(), t.AsProjString());
                    }

                    using (var t = ProjObject.Create(crs.AsWellKnownText()))
                    {
                        Assert.IsTrue(t is CoordinateReferenceSystem);

                        Assert.IsNotNull(t.Identifiers);
                        Assert.AreEqual("EPSG", t.Identifiers[0].Authority);
                        Assert.AreEqual("25832", t.Identifiers[0].Name);
                        Assert.AreEqual("+proj=utm +zone=32 +ellps=GRS80 +units=m +no_defs +type=crs", t.AsProjString());
                        Assert.AreEqual(crs.AsProjString(), t.AsProjString());

                        Assert.IsTrue(t.IsEquivalentTo(crs));
                    }
                    using (var t = ProjObject.Create(crs.AsProjJson()))
                    {
                        Assert.IsTrue(t is CoordinateReferenceSystem);

                        Assert.IsNotNull(t.Identifiers);
                        Assert.AreEqual("EPSG", t.Identifiers[0].Authority);
                        Assert.AreEqual("25832", t.Identifiers[0].Name);
                        Assert.AreEqual("+proj=utm +zone=32 +ellps=GRS80 +units=m +no_defs +type=crs", t.AsProjString());
                        Assert.AreEqual(crs.AsProjString(), t.AsProjString());
                        Assert.IsTrue(t.IsEquivalentTo(crs));
                    }


                }
            }
        }

        [TestMethod]
        public void CreateBasicTransform()
        {
            using (var pc = new ProjContext())
            {
                using (var crs1 = CoordinateReferenceSystem.Create("EPSG:25832", pc))
                using (var crs2 = CoordinateReferenceSystem.Create("EPSG:25833", pc))
                {
                    Assert.AreEqual(ProjType.ProjectedCrs, crs1.Type);
                    Assert.AreEqual(ProjType.ProjectedCrs, crs2.Type);

                    using (var t = CoordinateOperation.Create(crs1, crs2))
                    {
                        Assert.AreEqual("Inverse of UTM zone 32N + UTM zone 33N", t.Description);

                        using (var tr = t.CreateInverse())
                        {
                            Assert.AreEqual("Inverse of UTM zone 33N + UTM zone 32N", tr.Description);
                        }
                    }

                    using (var t = CoordinateOperation.Create(crs2, crs1))
                    {
                        Assert.AreEqual("Inverse of UTM zone 33N + UTM zone 32N", t.Description);
                    }
                }
            }
        }

        [TestMethod]
        public void TransformNL()
        {
            using (var pc = new ProjContext())
            {
                pc.LogLevel = ProjLogLevel.Error;
                using (var crs1 = CoordinateReferenceSystem.Create("EPSG:3857", pc))
                using (var crs2 = CoordinateReferenceSystem.Create("EPSG:23095", pc))
                using (var crs3 = CoordinateReferenceSystem.Create("EPSG:28992", pc))
                {
                    Assert.AreEqual("WGS 84 / Pseudo-Mercator", crs1.Description);
                    Assert.AreEqual("ED50 / TM 5 NE", crs2.Description);
                    Assert.AreEqual("Amersfoort / RD New", crs3.Description);

                    Assert.AreEqual(ProjType.ProjectedCrs, crs1.Type);
                    Assert.AreEqual(ProjType.ProjectedCrs, crs2.Type);
                    Assert.AreEqual(ProjType.ProjectedCrs, crs3.Type);

                    using (var c = crs3.GetGeodeticCoordinateReferenceSystem())
                    {
                        Assert.IsTrue((object)c is GeographicCoordinateReferenceSystem);
                        Assert.AreEqual("Amersfoort", c.Description);
                        Assert.AreEqual(ProjType.Geographic2DCrs, c.Type);
                    }
                    using (var d = crs3.GetDatum())
                    {
                        Assert.IsTrue((object)d is ProjDatum);
                        Assert.AreEqual("Amersfoort", d.Description);
                        Assert.AreEqual(ProjType.GeodeticReferenceFrame, d.Type);
                    }
                    using (var d = crs3.GetDatumList())
                    {
                        Assert.IsNull(d);
                    }
                    using (var d = crs3.GetDatumForced())
                    {
                        Assert.IsTrue((object)d is ProjDatum);
                        Assert.AreEqual("Amersfoort", d.Description);
                        Assert.AreEqual(ProjType.GeodeticReferenceFrame, d.Type);
                    }
                    using (var d = crs3.GetHorizontalDatum())
                    {
                        Assert.IsTrue((object)d is ProjDatum);
                        Assert.AreEqual("Amersfoort", d.Description);
                        Assert.AreEqual(ProjType.GeodeticReferenceFrame, d.Type);
                    }
                    using (var c = crs3.GetCoordinateSystem())
                    {
                        Assert.IsTrue((object)c is CoordinateSystem);
                        Assert.AreEqual(null, c.Description);
                        Assert.AreEqual(ProjType.Unknown, c.Type);
                        Assert.AreEqual(CoordinateSystemType.Cartesian, c.CsType);
                        Assert.AreEqual(2, c.AxisCount);
                        Assert.AreEqual(2, c.Axis.Count);

                        Assert.AreEqual("Easting", c.Axis[0].Name);
                        Assert.AreEqual("Northing", c.Axis[1].Name);
                        Assert.AreEqual("X", c.Axis[0].Abbreviation);
                        Assert.AreEqual("Y", c.Axis[1].Abbreviation);
                        Assert.AreEqual("metre", c.Axis[0].UnitName);
                        Assert.AreEqual("metre", c.Axis[1].UnitName);
                        Assert.AreEqual("EPSG", c.Axis[0].UnitAuthName);
                        Assert.AreEqual("EPSG", c.Axis[1].UnitAuthName);
                        Assert.AreEqual(1.0, c.Axis[0].UnitConversionFactor);
                        Assert.AreEqual(1.0, c.Axis[1].UnitConversionFactor);
                        Assert.AreEqual("9001", c.Axis[0].UnitCode);
                        Assert.AreEqual("9001", c.Axis[1].UnitCode);
                        Assert.AreEqual("east", c.Axis[0].Direction);
                        Assert.AreEqual("north", c.Axis[1].Direction);
                    }
                    using (var e = crs3.GetEllipsoid())
                    {
                        Assert.IsTrue((object)e is Ellipsoid);
                        Assert.AreEqual("Bessel 1841", e.Description);
                        Assert.AreEqual(ProjType.Ellipsoid, e.Type);

                        Assert.AreEqual(6377397.0, Math.Round(e.SemiMajorMetre, 0));
                        Assert.AreEqual(6356079.0, Math.Round(e.SemiMinorMetre, 0));
                        Assert.AreEqual(true, e.IsSemiMinorComputed);
                        Assert.AreEqual(299.0, Math.Round(e.InverseFlattening, 0));
                    }
                    using (var pm = crs3.GetPrimeMeridian())
                    {
                        Assert.IsTrue((object)pm is PrimeMeridian);
                        Assert.AreEqual("Greenwich", pm.Description);
                        Assert.AreEqual(0.0, pm.Longitude);
                        Assert.AreEqual(0.0175, Math.Round(pm.UnitConversionFactor, 4));
                        Assert.AreEqual("degree", pm.UnitName);
                    }
                    using (var co = crs3.GetCoordinateOperation())
                    {
                        Assert.IsTrue((object)co is CoordinateOperation);
                        Assert.AreEqual("RD New", co.Description);
                        //Assert.AreEqual(0.0, pm.Longitude);
                        //Assert.AreEqual(0.0175, Math.Round(pm.UnitConversionFactor, 4));
                        //Assert.AreEqual("degree", pm.UnitName);

                        //Assert.AreEqual(111129.0, Math.Round(co.E(new double[] { Proj.ToRad(r[0]), Proj.ToRad(r[1]) }, new double[] { Proj.ToRad(r[0]), Proj.ToRad(r[1] + 1) }), 0));
                        double[] r = new double[] { 5, 45 };
                        Assert.AreEqual(111129.0, Math.Round(co.EllipsoidDistance(new double[] { Proj.ToRad(r[0]), Proj.ToRad(r[1]) }, new double[] { Proj.ToRad(r[0]), Proj.ToRad(r[1] + 1) }), 0));
                    }

                    using (var t = CoordinateOperation.Create(crs1, crs2))
                    {
                        Assert.IsTrue(t is CoordinateOperationList);

                        var start = new double[] { Proj.ToRad(5.0), Proj.ToRad(52.0) };

                        var r = t.Transform(start);
                        GC.KeepAlive(r);

                        var s = t.InverseTransform(r);
                        GC.KeepAlive(s);



                        using (var c2 = crs1.GetGeodeticCoordinateReferenceSystem())
                        {
                            Assert.AreEqual("WGS 84", c2.Description);
                            Assert.AreEqual(ProjType.Geographic2DCrs, c2.Type);
                            using (var t2 = CoordinateOperation.Create(crs1, c2))
                            {
                                Assert.AreEqual("Inverse of Popular Visualisation Pseudo-Mercator", t2.Description);
                            }
                        }
                    }

                    Assert.AreEqual(ProjType.ProjectedCrs, crs1.Type);
                    Assert.AreEqual(ProjType.ProjectedCrs, crs2.Type);
                }
            }
        }

        [TestMethod]
        public void TestCopenhagen()
        {
            var ctx = new ProjContext();
            var src = CoordinateReferenceSystem.Create("EPSG:4326", ctx);
            var dst = CoordinateReferenceSystem.Create(/*"+proj=utm +zone=32 +datum=WGS84" or */ "EPSG:32632", ctx);
            Assert.AreEqual("WGS 84", src.Description);
            Assert.AreEqual("WGS 84 / UTM zone 32N", dst.Description);

            var t = CoordinateOperation.Create(src, dst, ctx);

            var t2 = t.CreateNormalized();


            var p = t2.Transform(new double[] { 12, 55 });

            Trace.WriteLine($"Easting: {p[0]}, Northing: {p[1]}");

            var r = t2.InverseTransform(p);

            Trace.WriteLine($"Longitude: {r[0]}, Latitude: {r[1]}");


            var tt = CoordinateOperation.Create(src, src, null);
            Assert.AreEqual("Null geographic offset from WGS 84 to WGS 84", tt.Description);

            var ss = ctx.Create("+proj=utm +zone=32 +datum=WGS84 +ellps=clrk66");
            //Assert.AreEqual(111334.0, Math.Round(ss.Distance2D(new double[] { Proj.ToRad(r[0]), Proj.ToRad(r[1]) }, new double[] { Proj.ToRad(r[0]), Proj.ToRad(r[1] + 1) }), 0));
        }

        [TestMethod]
        public void TestAmersfoort()
        {
            using (var c = new ProjContext())
            {
                using (var rd = CoordinateReferenceSystem.Create("EPSG:28992", c))
                using (var wgs84 = CoordinateReferenceSystem.Create("EPSG:4326", c))
                using (var google = CoordinateReferenceSystem.Create("EPSG:3857", c))
                {
                    var area = rd.UsageArea;

                    Assert.IsNotNull(area);
                    Assert.AreEqual("Netherlands - onshore, including Waddenzee, Dutch Wadden Islands and 12-mile offshore coastal zone.", area.Name);
                    Assert.AreEqual(3.2, area.WestLongitude);
                    Assert.AreEqual(7.22, area.EastLongitude);


                    using (var t = CoordinateOperation.Create(rd, wgs84))
                    {
                        var r = t.Transform(155000, 463000);
                        Assert.AreEqual(52.155, Math.Round(r[0], 3));
                        Assert.AreEqual(5.387, Math.Round(r[1], 3));

                        Assert.AreEqual(1, t.Accuraracy);
                    }

                    using (var t = CoordinateOperation.Create(rd, google))
                    {
                        var r = t.Transform(155000, 463000);

                        Assert.AreEqual(599701.0, Math.Round(r[0], 0));
                        Assert.AreEqual(6828231.0, Math.Round(r[1], 0));

                        Assert.AreEqual(1, t.Accuraracy);
                    }
                }
            }
        }

        [TestMethod]
        public void TestSpain()
        {
            using (var c = new ProjContext())
            {
                using (var wgs84 = CoordinateReferenceSystem.Create("EPSG:4326", c))
                using (var google = CoordinateReferenceSystem.Create("EPSG:3857", c))
                using (var q1 = CoordinateReferenceSystem.Create("EPSG:23030"))
                using (var q2 = CoordinateReferenceSystem.Create("EPSG:2062"))
                {
                    Assert.AreEqual("Engineering survey, topographic mapping.", q1.Scope);
                    Assert.AreEqual("Engineering survey, topographic mapping.", q2.Scope);

                    using (var t = CoordinateOperation.Create(google, wgs84))
                    {
                        var r = t.Transform(-333958.47, 4865942.28);
                        Assert.AreEqual(0, t.GridUsageCount);
                        Assert.AreEqual(40.0, Math.Round(r[0], 3));
                        Assert.AreEqual(-3, Math.Round(r[1], 3));
                    }

                    using (var t = CoordinateOperation.Create(google, q1))
                    {
                        var r = t.Transform(-333958.47, 4865942.28);
                        Assert.AreEqual(0, t.GridUsageCount);

                        Assert.AreEqual(500110.0, Math.Round(r[0], 0));
                        Assert.AreEqual(4427965.0, Math.Round(r[1], 0));
                    }

                    using (var t = CoordinateOperation.Create(google, q2))
                    {
                        var r = t.Transform(-333958.47, 4865942.28);
                        Assert.AreEqual(0, t.GridUsageCount);

                        Assert.AreEqual(658629.5, Math.Round(r[0], 1));
                        Assert.AreEqual(600226.1, Math.Round(r[1], 1));
                    }
                }
            }
        }

        [TestMethod]
        public void FewEpsg()
        {
            bool hasDeprecated = false;
            using (var wgs84 = CoordinateReferenceSystem.Create("EPSG:4326"))
            {
                for (int i = 2000; i < 2400; i++)
                {
                    CoordinateReferenceSystem crs;
                    try
                    {
                        crs = CoordinateReferenceSystem.Create($"EPSG:{i}");
                    }
                    catch (ProjException)
                    {
                        Trace.WriteLine($"Not supported: {i}");
                        //Assert.IsTrue(new int[] { 0, 1, 2,3 }.Contains(i), $"EPSG {i} not supported");
                        continue;
                    }

                    using (crs)
                    {
                        if (crs.IsDeprecated)
                        {
                            hasDeprecated = true;
                            continue;
                        }

                        CoordinateOperation t;
                        try
                        {
                            t = CoordinateOperation.Create(wgs84, crs);
                        }
                        catch (ProjException)
                        {
                            Trace.WriteLine($"Not convertible: {i}");
                            //Assert.IsTrue(new int[] { 0, 1, 2,3 }.Contains(i), $"EPSG {i} not supported");
                            continue;
                        }


                        using (t)
                        {
                            var a = crs.UsageArea;

                            double[] center;
                            try
                            {
                                center = t.Transform((a.EastLongitude + a.WestLongitude) / 2, (a.NorthLatitude + a.SouthLatitude) / 2);
                            }
                            catch (ProjException)
                            {
                                center = null;
                            }


                            if (center != null && t.HasInverse && !(t is CoordinateOperationList))
                            {
                                double[] ret = t.InverseTransform(center);
                            }
                        }
                    }
                }
            }
            Assert.IsTrue(hasDeprecated, "Found deprecated");
        }

        [TestMethod]
        public void WithGrid()
        {
            using (var pc = new ProjContext())
            {
                // Don't use old cache
                pc.SetGridCache(true, Path.Combine(TestContext.TestResultsDirectory, "proj.cache"), 300, 3600 * 24);
                pc.LogLevel = ProjLogLevel.Trace;

                using (var crsAmersfoort = CoordinateReferenceSystem.Create(@"EPSG:4289", pc)) // Amersfoort
                using (var crsETRS89 = CoordinateReferenceSystem.Create(@"EPSG:4258", pc))
                {
                    // Do it the dumb way
                    using (var t = CoordinateOperation.Create(crsAmersfoort, crsETRS89))
                    {
                        Assert.IsFalse(t is CoordinateOperationList);
                        var r = t.Transform(51, 4, 0);

                        Assert.AreEqual(50.999, Math.Round(r[0], 3));
                        Assert.AreEqual(4.0, Math.Round(r[1], 3));
                        Assert.AreEqual(0.0, Math.Round(r[2], 3));
                    }

                    // Now, let's enable gridshifts
                    Assert.IsFalse(pc.AllowNetworkConnections);
                    pc.AllowNetworkConnections = true;
                    pc.EndpointUrl = "https://cdn.proj.org";
                    bool usedHttp = false;
                    pc.Log += (_, x) => { if (x.Contains("https://")) usedHttp = true; };

                    using (var t = CoordinateOperation.Create(crsAmersfoort, crsETRS89))
                    {
                        CoordinateOperationList cl = t as CoordinateOperationList;
                        Assert.IsNotNull(cl);
                        Assert.AreEqual(2, cl.Count);

                        Assert.IsTrue(cl[0].GridUsageCount > 0);
                        Assert.IsTrue(cl[1].GridUsageCount == 0);

                        var r = t.Transform(51, 4, 0);
                        Assert.IsTrue(usedHttp, "Now http");
                        Assert.AreEqual(50.999, Math.Round(r[0], 3));
                        Assert.AreEqual(4.0, Math.Round(r[1], 3));
                        Assert.AreEqual(0.0, Math.Round(r[2], 3));


                        var r0 = cl[0].Transform(51, 4, 0);
                        usedHttp = false;
                        var r1 = cl[1].Transform(51, 4, 0);
                        Assert.IsFalse(usedHttp, "No http");
                        Assert.IsNotNull(r0);
                        Assert.IsNotNull(r1);

                        Assert.AreEqual(50.999, Math.Round(r0[0], 3));
                        Assert.AreEqual(4.0, Math.Round(r0[1], 3));
                        Assert.AreEqual(0.0, Math.Round(r0[2], 3));

                        Assert.AreEqual(50.999, Math.Round(r1[0], 3));
                        Assert.AreEqual(4.0, Math.Round(r1[1], 3));
                        Assert.AreEqual(0.0, Math.Round(r1[2], 3));

                    }
                }
            }
        }
    }
}
