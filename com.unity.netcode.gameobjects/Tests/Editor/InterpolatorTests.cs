using System;
using NUnit.Framework;

namespace Unity.Netcode.EditorTests
{
    internal class InterpolatorTests
    {
        private const float k_Precision = 0.00000001f;
        private const int k_MockTickRate = 1;

        private NetworkTime T(float time, uint tickRate = k_MockTickRate)
        {
            return new NetworkTime(tickRate, timeSec: time);
        }

        [Test]
        public void TestReset()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();

            var serverTime = new NetworkTime(k_MockTickRate, 100f);
            interpolator.AddMeasurement(5, 1.0f);
            var initVal = interpolator.Update(10f, serverTime.Time, serverTime.TimeTicksAgo(1).Time); // big value
            Assert.That(initVal, Is.EqualTo(5f));
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(5f));

            interpolator.ResetTo(100f, serverTime.Time);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(100f));
            var val = interpolator.Update(1f, serverTime.Time, serverTime.TimeTicksAgo(1).Time);
            Assert.That(val, Is.EqualTo(100f));
        }

        [Test]
        public void NormalUsage()
        {
            // Testing float instead of Vector3. The only difference with Vector3 is the lerp method used.
            var interpolator = new BufferedLinearInterpolatorFloat();

            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0f));

            interpolator.AddMeasurement(0f, 1.0f);
            interpolator.AddMeasurement(1f, 2.0f);

            // too small update, nothing happens, doesn't consume from buffer yet
            var serverTime = new NetworkTime(k_MockTickRate, 0.01d); // t = 0.1d
            interpolator.Update(.01f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0f));

            // consume first measurement, still can't interpolate with just one tick consumed
            serverTime += 1.0d; // t = 1.01
            interpolator.Update(1.0f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0f));

            // consume second measurement, start to interpolate
            serverTime += 1.0d; // t = 2.01
            var valueFromUpdate = interpolator.Update(1.0f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0.01f).Within(k_Precision));
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0.01f).Within(k_Precision)); // test a second time, to make sure the get doesn't update the value
            Assert.That(valueFromUpdate, Is.EqualTo(interpolator.GetInterpolatedValue()).Within(k_Precision));

            // continue interpolation
            serverTime = new NetworkTime(k_MockTickRate, 2.5d); // t = 2.5d
            interpolator.Update(2.5f - 2.01f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0.5f).Within(k_Precision));

            // check when reaching end
            serverTime += 0.5d; // t = 3
            interpolator.Update(0.5f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1f).Within(k_Precision));
        }

        /// <summary>
        /// Out of order or 'ACB' problem
        /// Given two measurements have already arrived A and C, if a new measurement B arrives, the interpolation shouldn't go to B, but continue
        /// to C.
        /// Adding B should be ignored if interpolation is already interpolating between A and C
        /// </summary>
        [Test]
        public void OutOfOrderShouldStillWork()
        {
            var serverTime = new NetworkTime(k_MockTickRate, 0.01d);
            var interpolator = new BufferedLinearInterpolatorFloat();
            double timeStep = 0.5d;

            interpolator.AddMeasurement(0f, 0d);
            interpolator.AddMeasurement(2f, 2d);

            serverTime = new NetworkTime(k_MockTickRate, 1.5d);
            interpolator.Update(1.5f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(0f).Within(k_Precision));

            serverTime += timeStep; // t = 2.0
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1f).Within(k_Precision));

            serverTime += timeStep; // t = 2.5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1.5f).Within(k_Precision));

            // makes sure that interpolation still continues in right direction
            interpolator.AddMeasurement(1, 1d);

            serverTime += timeStep; // t = 3
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(2f).Within(k_Precision));
        }

        [Ignore("TODO: Fix this test to still handle testing message loss without extrapolation")]
        [Test]
        public void MessageLoss()
        {
            var serverTime = new NetworkTime(k_MockTickRate, 0.01d);
            var interpolator = new BufferedLinearInterpolatorFloat();
            double timeStep = 0.5d;

            interpolator.AddMeasurement(1f, 1d);
            interpolator.AddMeasurement(2f, 2d);
            // message time=3 was lost
            interpolator.AddMeasurement(4f, 4d);
            interpolator.AddMeasurement(5f, 5d);
            // message time=6 was lost
            interpolator.AddMeasurement(100f, 7d); // high value to produce a misprediction

            // first value teleports interpolator
            serverTime = new NetworkTime(k_MockTickRate, 1d);
            interpolator.Update(1f, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1f));

            // nothing happens, not ready to consume second value yet
            serverTime += timeStep;  // t = 1.5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1f));

            // beginning of interpolation, second value consumed, currently at start
            serverTime += timeStep; // t = 2
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1f));

            // interpolation starts
            serverTime += timeStep; // t = 2.5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(1.5f));

            serverTime += timeStep; // t = 3
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(2f));

            // extrapolating to 2.5
            serverTime += timeStep; // t = 3.5d
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(2.5f));

            // next value skips to where it was supposed to be once buffer time is showing the next value
            serverTime += timeStep; // t = 4
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(3f));

            // interpolation continues as expected
            serverTime += timeStep; // t = 4.5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(3.5f));

            serverTime += timeStep; // t = 5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(4f));

            // lost time=6, extrapolating
            serverTime += timeStep; // t = 5.5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(4.5f));

            serverTime += timeStep; // t = 6.0
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(5f));

            // misprediction
            serverTime += timeStep; // t = 6.5
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.EqualTo(5.5f));

            // lerp to right value
            serverTime += timeStep; // t = 7.0
            interpolator.Update((float)timeStep, serverTime);
            Assert.That(interpolator.GetInterpolatedValue(), Is.GreaterThan(6.0f));
            Assert.That(interpolator.GetInterpolatedValue(), Is.LessThanOrEqualTo(100f));
        }

        [Test]
        public void AddFirstMeasurement()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();

            var serverTime = new NetworkTime(k_MockTickRate, 0d);
            interpolator.AddMeasurement(2f, 1d);
            interpolator.AddMeasurement(3f, 2d);

            serverTime += 1d; // t = 1
            var interpolatedValue = interpolator.Update(1f, serverTime);
            // when consuming only one measurement and it's the first one consumed, teleport to it
            Assert.That(interpolatedValue, Is.EqualTo(2f));

            // then interpolation should work as usual
            serverTime += 1d; // t = 2
            interpolatedValue = interpolator.Update(1f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(2f));

            serverTime += 0.5d; // t = 2.5
            interpolatedValue = interpolator.Update(0.5f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(2.5f));

            serverTime += 0.5d; // t = 3
            interpolatedValue = interpolator.Update(.5f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(3f));
        }

        [Test]
        public void JumpToEachValueIfDeltaTimeTooBig()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();

            var serverTime = new NetworkTime(k_MockTickRate, 0d);
            interpolator.AddMeasurement(2f, 1d);
            interpolator.AddMeasurement(3f, 2d);

            serverTime += 1d; // t = 1
            var interpolatedValue = interpolator.Update(1f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(2f));

            // big deltaTime, jumping to latest value
            serverTime += 9f; // t = 10
            interpolatedValue = interpolator.Update(8f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(3));
        }

        [Test]
        public void JumpToLastValueFromStart()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();

            var serverTime = new NetworkTime(k_MockTickRate, 0d);

            serverTime += 1d; // t = 1
            interpolator.AddMeasurement(1f, serverTime.Time);
            serverTime += 1d; // t = 2
            interpolator.AddMeasurement(2f, serverTime.Time);
            serverTime += 1d; // t = 3
            interpolator.AddMeasurement(3f, serverTime.Time);

            // big time jump
            serverTime += 7d; // t = 10
            var interpolatedValue = interpolator.Update(10f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(3f));

            // interpolation continues as normal
            serverTime = new NetworkTime(k_MockTickRate, 11d); // t = 11
            interpolator.AddMeasurement(11f, serverTime.Time); // out of order

            serverTime = new NetworkTime(k_MockTickRate, 10.5d); // t = 10.5
            interpolatedValue = interpolator.Update(0.5f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(3f));

            serverTime += 0.5d; // t = 11
            interpolatedValue = interpolator.Update(0.5f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(10f));

            serverTime += 0.5d; // t = 11.5
            interpolatedValue = interpolator.Update(0.5f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(10.5f));

            serverTime += 0.5d; // t = 12
            interpolatedValue = interpolator.Update(0.5f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(11f));
        }

        [Test]
        public void TestBufferSizeLimit()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();

            // set first value
            var serverTime = new NetworkTime(k_MockTickRate, 0d);
            serverTime += 1.0d; // t = 1
            interpolator.AddMeasurement(-1f, serverTime.Time);
            interpolator.Update(1f, serverTime);

            // max + 1
            serverTime += 1.0d; // t = 2
            interpolator.AddMeasurement(2, serverTime.Time); // +1, this should trigger a burst and teleport to last value
            for (int i = 0; i < 100; i++)
            {
                interpolator.AddMeasurement(i + 3, i + 3d);
            }

            // client was paused for a while, some time has past, we just got a burst of values from the server that teleported us to the last value received
            serverTime = new NetworkTime(k_MockTickRate, 102d);
            var interpolatedValue = interpolator.Update(101f, serverTime);
            Assert.That(interpolatedValue, Is.EqualTo(102));
        }

        [Test]
        public void TestUpdatingInterpolatorWithNoData()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();
            var serverTime = new NetworkTime(k_MockTickRate, 0.0d);
            // invalid case, this is undefined behaviour
            Assert.Throws<InvalidOperationException>(() => interpolator.Update(1f, serverTime));
        }

        [Ignore("TODO: Fix this test to still test duplicated values without extrapolation")]
        [Test]
        public void TestDuplicatedValues()
        {
            var interpolator = new BufferedLinearInterpolatorFloat();

            var serverTime = new NetworkTime(k_MockTickRate, 0.0d);

            serverTime += 1d; // t = 1
            interpolator.AddMeasurement(1f, serverTime.Time);
            serverTime += 1d; // t = 2
            interpolator.AddMeasurement(2f, serverTime.Time);
            interpolator.AddMeasurement(2f, serverTime.Time);

            // empty interpolator teleports to initial value
            serverTime = new NetworkTime(k_MockTickRate, 0.0d);
            serverTime += 1d; // t = 1
            var interp = interpolator.Update(1f, serverTime);
            Assert.That(interp, Is.EqualTo(1f));

            // consume value, start interp, currently at start value
            serverTime += 1d; // t = 2
            interp = interpolator.Update(1f, serverTime);
            Assert.That(interp, Is.EqualTo(1f));

            // interp
            serverTime += 0.5d; // t = 2.5
            interp = interpolator.Update(0.5f, serverTime);
            Assert.That(interp, Is.EqualTo(1.5f));

            // reach end
            serverTime += 0.5d; // t = 3
            interp = interpolator.Update(0.5f, serverTime);
            Assert.That(interp, Is.EqualTo(2f));

            // with unclamped interpolation, we continue mispredicting since the two last values are actually treated as the same. Therefore we're not stopping at "2"
            serverTime += 0.5d; // t = 3.5
            interp = interpolator.Update(0.5f, serverTime);
            Assert.That(interp, Is.EqualTo(2.5f));

            serverTime += 0.5d; // t = 4
            interp = interpolator.Update(0.5f, serverTime);
            Assert.That(interp, Is.EqualTo(3f));

            // we add a measurement with an updated time
            var pastServerTime = new NetworkTime(k_MockTickRate, 3.0d);
            interpolator.AddMeasurement(2f, pastServerTime.Time);

            interp = interpolator.Update(0.5f, serverTime);
            Assert.That(interp, Is.EqualTo(2f));
        }
    }
}
