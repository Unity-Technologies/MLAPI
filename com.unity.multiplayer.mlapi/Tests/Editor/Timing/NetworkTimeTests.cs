using System;
using System.Collections.Generic;
using System.Linq;
using MLAPI.Timing;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace MLAPI.EditorTests.Timing
{
    public class NetworkTimeTests
    {

        [Test]
        public void NetworkTimeCreate()
        {
            double a = 34d;
            double b = 17.32d;
            double c = -42.44d;
            double d = -6d;
            double e = int.MaxValue / 61d;

            var timeA = new NetworkTime(60, a);
            var timeB = new NetworkTime(60, b);
            var timeC = new NetworkTime(60, c);
            var timeD = new NetworkTime(60, d);
            var timeE = new NetworkTime(60, e);

            Assert.IsTrue(Approximately(a, timeA.Time));
            Assert.IsTrue(Approximately(b, timeB.Time));
            Assert.IsTrue(Approximately(c, timeC.Time));
            Assert.IsTrue(Approximately(d, timeD.Time));
            Assert.IsTrue(Approximately(e, timeE.Time));

            Assert.IsTrue(Approximately(timeA.Tick * timeA.FixedDeltaTime + timeA.TickOffset, timeA.Time, 0.0001d));
            Assert.IsTrue(Approximately(timeB.Tick * timeB.FixedDeltaTime + timeB.TickOffset, timeB.Time, 0.0001d));
            Assert.IsTrue(Approximately(timeC.Tick * timeC.FixedDeltaTime + timeC.TickOffset, timeC.Time, 0.0001d));
            Assert.IsTrue(Approximately(timeD.Tick * timeD.FixedDeltaTime + timeD.TickOffset, timeD.Time, 0.0001d));
            Assert.IsTrue(Approximately(timeE.Tick * timeE.FixedDeltaTime + timeE.TickOffset, timeE.Time, 10d));

            Assert.IsTrue(Approximately(timeA.TickOffset, 0));
            Assert.IsTrue(Approximately(timeB.TickOffset, 0.2d / 60d));
            Assert.IsTrue(Approximately(timeC.TickOffset, 1d / 60d - 0.4d / 60d));
            Assert.IsTrue(Approximately(timeD.TickOffset, 0));
            Assert.IsTrue(Approximately(timeE.TickOffset, 0.00082)); // Int.Max / 61 / (1/60) to get divisor then: Int.Max - divisor * 1 / 60
        }

        [Test]
        public void NetworkTimeDefault()
        {
            NetworkTime defaultTime = default;

            Assert.IsTrue(defaultTime.Time == 0f);
        }

        [Test]
        public void NetworkTimeAddFloatTest()
        {
            double a = 34d;
            double b = 17.32d;
            double c = -42.4d;
            double d = -6d;
            double e = int.MaxValue / 61d;

            double floatResultB = a + b;
            double floatResultC = a + c;
            double floatResultD = a + d;
            double floatResultE = a + e;

            var timeA = new NetworkTime(60, a);
            NetworkTime timeB = timeA + b;
            NetworkTime timeC = timeA + c;
            NetworkTime timeD = timeA + d;
            NetworkTime timeE = timeA + e;

            Assert.IsTrue(Approximately(floatResultB, timeB.Time));
            Assert.IsTrue(Approximately(floatResultC, timeC.Time));
            Assert.IsTrue(Approximately(floatResultD, timeD.Time));
            Assert.IsTrue(Approximately(floatResultE, timeE.Time));
        }

        [Test]
        public void NetworkTimeSubFloatTest()
        {
            double a = 34d;
            double b = 17.32d;
            double c = -42.4d;
            double d = -6d;
            double e = int.MaxValue / 61d;

            double floatResultB = a - b;
            double floatResultC = a - c;
            double floatResultD = a - d;
            double floatResultE = a - e;

            var timeA = new NetworkTime(60, a);
            NetworkTime timeB = timeA - b;
            NetworkTime timeC = timeA - c;
            NetworkTime timeD = timeA - d;
            NetworkTime timeE = timeA - e;

            Assert.IsTrue(Approximately(floatResultB, timeB.Time));
            Assert.IsTrue(Approximately(floatResultC, timeC.Time));
            Assert.IsTrue(Approximately(floatResultD, timeD.Time));
            Assert.IsTrue(Approximately(floatResultE, timeE.Time));
        }


        [Test]
        public void NetworkTimeAddNetworkTimeTest()
        {
            double a = 34d;
            double b = 17.32d;
            double c = -42.4d;
            double d = -6d;
            double e = int.MaxValue / 61d;

            double floatResultB = a + b;
            double floatResultC = a + c;
            double floatResultD = a + d;
            double floatResultE = a + e;

            var timeA = new NetworkTime(60, a);
            NetworkTime timeB = timeA + new NetworkTime(60, b);
            NetworkTime timeC = timeA + new NetworkTime(60, c);
            NetworkTime timeD = timeA + new NetworkTime(60, d);
            NetworkTime timeE = timeA + new NetworkTime(60, e);

            Assert.IsTrue(Approximately(floatResultB, timeB.Time));
            Assert.IsTrue(Approximately(floatResultC, timeC.Time));
            Assert.IsTrue(Approximately(floatResultD, timeD.Time));
            Assert.IsTrue(Approximately(floatResultE, timeE.Time));
        }

        [Test]
        public void NetworkTimeSubNetworkTimeTest()
        {
            double a = 34d;
            double b = 17.32d;
            double c = -42.4d;
            double d = -6d;
            double e = int.MaxValue / 61d;

            double floatResultB = a - b;
            double floatResultC = a - c;
            double floatResultD = a - d;
            double floatResultE = a - e;

            var timeA = new NetworkTime(60, a);
            NetworkTime timeB = timeA - new NetworkTime(60, b);
            NetworkTime timeC = timeA - new NetworkTime(60, c);
            NetworkTime timeD = timeA - new NetworkTime(60, d);
            NetworkTime timeE = timeA - new NetworkTime(60, e);

            Assert.IsTrue(Approximately(floatResultB, timeB.Time));
            Assert.IsTrue(Approximately(floatResultC, timeC.Time));
            Assert.IsTrue(Approximately(floatResultD, timeD.Time));
            Assert.IsTrue(Approximately(floatResultE, timeE.Time));
        }

        [Test]
        public void NetworkTimeAdvanceTest()
        {
            var random = new Random(42);
            var randomSteps = Enumerable.Repeat(0f, 1000).Select(t => Mathf.Lerp(1 / 25f, 1.80f, (float)random.NextDouble())).ToList();

            NetworkTimeAdvanceTestInternal(randomSteps, 60, 0f);
            NetworkTimeAdvanceTestInternal(randomSteps, 1, 0f);
            NetworkTimeAdvanceTestInternal(randomSteps, 10, 0f);
            NetworkTimeAdvanceTestInternal(randomSteps, 20, 0f);
            NetworkTimeAdvanceTestInternal(randomSteps, 30, 0f);
            NetworkTimeAdvanceTestInternal(randomSteps, 144, 0f);


            NetworkTimeAdvanceTestInternal(randomSteps, 60, 23132.231f);
            NetworkTimeAdvanceTestInternal(randomSteps, 1, 23132.231f);
            NetworkTimeAdvanceTestInternal(randomSteps, 10, 23132.231f);
            NetworkTimeAdvanceTestInternal(randomSteps, 20, 23132.231f);
            NetworkTimeAdvanceTestInternal(randomSteps, 30, 23132.231f);
            NetworkTimeAdvanceTestInternal(randomSteps, 30, 23132.231f);
            NetworkTimeAdvanceTestInternal(randomSteps, 144, 23132.231f);

            var shortSteps = Enumerable.Repeat(1 / 30f, 1000);

            NetworkTimeAdvanceTestInternal(shortSteps, 60, 0f);
            NetworkTimeAdvanceTestInternal(shortSteps, 1, 0f);
            NetworkTimeAdvanceTestInternal(shortSteps, 10, 0f);
            NetworkTimeAdvanceTestInternal(shortSteps, 20, 0f);
            NetworkTimeAdvanceTestInternal(shortSteps, 30, 0f);
            NetworkTimeAdvanceTestInternal(shortSteps, 144, 0f);

            NetworkTimeAdvanceTestInternal(shortSteps, 60, 1000000f);
            NetworkTimeAdvanceTestInternal(shortSteps, 60, 1000000f);
            NetworkTimeAdvanceTestInternal(shortSteps, 1, 1000000f);
            NetworkTimeAdvanceTestInternal(shortSteps, 10, 1000000f);
            NetworkTimeAdvanceTestInternal(shortSteps, 20, 1000000f);
            NetworkTimeAdvanceTestInternal(shortSteps, 30, 1000000f);
            NetworkTimeAdvanceTestInternal(shortSteps, 144, 1000000f);
        }

        private void NetworkTimeAdvanceTestInternal(IEnumerable<float> steps, int tickRate, float start, float start2 = 0f)
        {
            float maxAcceptableTotalOffset = 0.005f;

            var startTime = new NetworkTime(tickRate, start);
            var startTime2 = new NetworkTime(tickRate, start2);
            NetworkTime dif = startTime2 - startTime;

            foreach (var step in steps)
            {
                startTime += step;
                startTime2 += step;
                Assert.IsTrue(Approximately(startTime.Time, (startTime2 - dif).Time));
            }

            Assert.IsTrue(Approximately(startTime.Time, (startTime2 - dif).Time, maxAcceptableTotalOffset));
        }

        private static bool Approximately(double a, double b, double epsilon = 0.000001d)
        {
            var dif = Math.Abs(a - b);
            return dif <= epsilon;
        }

    }
}
