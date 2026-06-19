using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NorthStar.Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="EventBus"/> covering subscribe/publish/unsubscribe
    /// and per-handler exception isolation. EventBus is global static state, so each test
    /// clears it in setup/teardown to stay independent.
    /// </summary>
    public class EventBusTests
    {
        // A small test-only event type so these tests never collide with gameplay events.
        private struct TestEvent
        {
            public int value;
        }

        private struct OtherTestEvent
        {
            public string label;
        }

        [SetUp]
        public void SetUp() => EventBus.ClearAll();

        [TearDown]
        public void TearDown() => EventBus.ClearAll();

        [Test]
        public void Publish_InvokesSubscribedHandler_WithPayload()
        {
            int received = 0;
            Action<TestEvent> handler = e => received = e.value;

            EventBus.Subscribe(handler);
            EventBus.Publish(new TestEvent { value = 42 });

            Assert.AreEqual(42, received, "Subscribed handler should receive the published payload.");
        }

        [Test]
        public void Publish_InvokesAllSubscribersForType()
        {
            int callCount = 0;
            Action<TestEvent> a = _ => callCount++;
            Action<TestEvent> b = _ => callCount++;
            Action<TestEvent> c = _ => callCount++;

            EventBus.Subscribe(a);
            EventBus.Subscribe(b);
            EventBus.Subscribe(c);
            EventBus.Publish(new TestEvent { value = 1 });

            Assert.AreEqual(3, callCount, "Every subscriber for the event type should be invoked once.");
        }

        [Test]
        public void Publish_DoesNotInvokeHandlersOfOtherEventTypes()
        {
            bool otherCalled = false;
            Action<OtherTestEvent> other = _ => otherCalled = true;

            EventBus.Subscribe(other);
            EventBus.Publish(new TestEvent { value = 7 });

            Assert.IsFalse(otherCalled, "Publishing one type must not invoke handlers of a different type.");
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent { value = 99 }));
        }

        [Test]
        public void Unsubscribe_StopsHandlerFromReceivingEvents()
        {
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Publish(new TestEvent { value = 1 });
            EventBus.Unsubscribe(handler);
            EventBus.Publish(new TestEvent { value = 2 });

            Assert.AreEqual(1, callCount, "Handler should only have fired before it was unsubscribed.");
        }

        [Test]
        public void Unsubscribe_UnknownHandler_DoesNotThrow()
        {
            Action<TestEvent> handler = _ => { };
            Assert.DoesNotThrow(() => EventBus.Unsubscribe(handler),
                "Unsubscribing a handler that was never subscribed must be a safe no-op.");
        }

        [Test]
        public void Subscribe_NullHandler_IsIgnored()
        {
            Assert.DoesNotThrow(() => EventBus.Subscribe<TestEvent>(null));
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent { value = 5 }));
        }

        [Test]
        public void ThrowingSubscriber_DoesNotPreventOtherSubscribers()
        {
            // Per the contract: each handler is isolated so one throwing subscriber
            // cannot break the others. The bus logs the exception via Debug.LogError.
            bool firstCalled = false;
            bool thirdCalled = false;

            Action<TestEvent> first = _ => firstCalled = true;
            Action<TestEvent> throwing = _ => throw new InvalidOperationException("boom");
            Action<TestEvent> third = _ => thirdCalled = true;

            EventBus.Subscribe(first);
            EventBus.Subscribe(throwing);
            EventBus.Subscribe(third);

            // Expect the isolated error to be logged so the test runner does not fail on it.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("EventBus.*boom"));

            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent { value = 1 }),
                "Publish must swallow per-handler exceptions.");

            Assert.IsTrue(firstCalled, "Subscriber before the throwing one should still run.");
            Assert.IsTrue(thirdCalled, "Subscriber after the throwing one should still run.");
        }

        [Test]
        public void ClearAll_RemovesAllSubscribers()
        {
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;

            EventBus.Subscribe(handler);
            EventBus.ClearAll();
            EventBus.Publish(new TestEvent { value = 1 });

            Assert.AreEqual(0, callCount, "ClearAll should remove every subscription.");
        }
    }
}
