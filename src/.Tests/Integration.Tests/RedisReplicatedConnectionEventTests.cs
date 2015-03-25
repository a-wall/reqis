using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StackExchange.Redis;

namespace Integration.Tests
{
    [TestFixture]
    public class RedisReplicatedConnectionEventTests
    {
        private ConnectionMultiplexer _redis;
        private IObservable<EventPattern<ConnectionFailedEventArgs>> _connectionFailed;
        private IObservable<EventPattern<ConnectionFailedEventArgs>> _connectionRestored;
        private IObservable<EventPattern<RedisErrorEventArgs>> _errorMessage;
        private IObservable<EventPattern<InternalErrorEventArgs>> _internalError;

        [SetUp]
        public void SetUp()
        {
            var config = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                EndPoints = { { "localhost", 6379 }, { "localhost", 6380 } }
            };
            _redis = ConnectionMultiplexer.Connect(config);
            Debug.WriteLine("Connection made to: {0}", _redis);
            _connectionFailed = Observable.FromEventPattern<ConnectionFailedEventArgs>(
                add => _redis.ConnectionFailed += add, rem => _redis.ConnectionFailed -= rem)
                .Do(p => Debug.WriteLine("Connection failed: {0}", p.EventArgs.FailureType));
            _connectionRestored = Observable.FromEventPattern<ConnectionFailedEventArgs>(
                add => _redis.ConnectionRestored += add, rem => _redis.ConnectionRestored -= rem)
                .Do(p => Debug.WriteLine("Connection restored: {0}", p.EventArgs.FailureType));
            _errorMessage = Observable.FromEventPattern<RedisErrorEventArgs>(add => _redis.ErrorMessage += add,
                rem => _redis.ErrorMessage -= rem)
                .Do(p => Debug.WriteLine("Error message: {0}", p.EventArgs));
            _internalError = Observable.FromEventPattern<InternalErrorEventArgs>(add => _redis.InternalError += add,
                rem => _redis.InternalError -= rem)
                .Do(p => Debug.WriteLine("Internal error: {0}", p.EventArgs.Exception));
        }

        [TearDown]
        public void TearDown()
        {
            _redis.Dispose();
        }

        [Test]
        public void RestoreConnectionTest()
        {
            var evt = new ManualResetEventSlim();
            var failed = false;
            var restored = false;

            var d = new CompositeDisposable
            {
                _connectionFailed.Subscribe(p => { failed = true; }),
                _connectionRestored.Subscribe(p =>
                {
                    restored = true;
                    evt.Set();
                }),
                _errorMessage.Subscribe(p => { }),
                _internalError.Subscribe(p => { }),
            };

            evt.Wait(TimeSpan.FromSeconds(600)); // at this point manually stop and restart the redis process
            d.Dispose();
            Assert.IsTrue(failed);
            Assert.IsTrue(restored);
        }

        [Test]
        public void RestorePubSubTest()
        {
            // setup publisher
            var sub = _redis.GetSubscriber();
            var outgoing = Observable.Interval(TimeSpan.FromSeconds(1));

            // setup subscriber
            var incoming = Observable.Create<long>(observer =>
            {
                sub.Subscribe("counter", (channel, message) => observer.OnNext((long) message));
                return Disposable.Create(() => { });
            });

            var evt = new ManualResetEventSlim();
            var failed = false;
            var restored = false;
            long count = 0;

            var d = new CompositeDisposable
            {
                outgoing.Subscribe(l =>
                {
                    try
                    {
                        sub.Publish("counter", l);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Unable to publish beacuase: {0} {1}", ex.Message, string.Empty);
                    }
                }),
                incoming.Subscribe(l =>
                {
                    count = l;
                    Debug.WriteLine("counter {0}", l);
                    if (restored) evt.Set();
                }),
                _connectionFailed.Subscribe(p => { failed = true; }),
                _connectionRestored.Subscribe(p => { restored = true; }),
                _errorMessage.Subscribe(p => { }),
                _internalError.Subscribe(p => { }),
            };

            evt.Wait(TimeSpan.FromSeconds(600));
            d.Dispose();
            Assert.IsTrue(failed);
            Assert.IsTrue(restored);
            Assert.GreaterOrEqual(count, 1);
        }
    }
}
