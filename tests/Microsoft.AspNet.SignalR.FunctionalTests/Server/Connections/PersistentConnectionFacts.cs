using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client.Http;
using Microsoft.AspNet.SignalR.Configuration;
using Microsoft.AspNet.SignalR.FunctionalTests;
using Microsoft.AspNet.SignalR.Hosting.Memory;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Tests.Common;
using Microsoft.AspNet.SignalR.Tests.Common.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Owin;
using Xunit;
using Xunit.Extensions;

namespace Microsoft.AspNet.SignalR.Tests
{
    public class PersistentConnectionFacts
    {
        private static int _requestId;

        public class OnConnectedAsync : HostedTest
        {
            [Theory]
            [InlineData()]
            [InlineData()]
            [InlineData()]
            [InlineData()]
            [InlineData()]
            public void ConnectionsWithTheSameConnectionIdSSECloseGracefully()
            {
                Debug.Listeners.Clear();
                using (var host = new MemoryHost())
                {
                    host.Configure(app =>
                    {
                        var config = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        app.MapSignalR<MyGroupEchoConnection>("/echo", config);

                        config.Resolver.Register(typeof(IProtectedData), () => new EmptyProtectedData());
                    });

                    string id = Guid.NewGuid().ToString("d");

                    var tasks = new Task[1000];

                    for (int i = 0; i < 1000; i++)
                    {
                        tasks[i] = ProcessRequest(host, "serverSentEvents", id);
                    }

                    var finalRequest = ProcessRequest(host, "serverSentEvents", id);

                    try
                    {
                        Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(30)));
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine(string.Join(", ", tasks.Select(t => t.Status)));
                        throw;
                    }

                    Assert.True(tasks.All(t => t.Status == TaskStatus.RanToCompletion));
                    Assert.False(finalRequest.IsCompleted);
                }
            }

            [Theory]
            [InlineData()]
            [InlineData()]
            [InlineData()]
            [InlineData()]
            [InlineData()]
            public void ConnectionsWithTheSameConnectionIdLongPollingCloseGracefully()
            {
                Debug.Listeners.Clear();
                using (var host = new MemoryHost())
                {
                    host.Configure(app =>
                    {
                        var config = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        app.MapSignalR<MyGroupEchoConnection>("/echo", config);

                        config.Resolver.Register(typeof(IProtectedData), () => new EmptyProtectedData());
                    });

                    string id = Guid.NewGuid().ToString("d");

                    var tasks = new Task[1000];

                    for (int i = 0; i < 1000; i++)
                    {
                        tasks[i] = ProcessRequest(host, "longPolling", id);
                    }

                    var finalRequest = ProcessRequest(host, "longPolling", id);

                    try
                    {
                        Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(30)));
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine(string.Join(", ", tasks.Select(t => t.Status)));
                        throw;
                    }

                    Assert.True(tasks.All(t => t.Status == TaskStatus.RanToCompletion));
                    //Assert.False(finalRequest.IsCompleted);
                }
            }

            private static async Task<string> ProcessRequest(MemoryHost host, string transport, string connectionToken)
            {
                var requestId = _requestId++;
                Debug.WriteLine("ProcessRequest({0})", requestId);
                var response = await host.Get("http://foo/echo/connect?transport=" + transport + "&connectionToken=" + connectionToken + "&requestId=" + requestId, r => { }, isLongRunning: true);
                Debug.WriteLine("ProcessRequest({0}) Response Began", requestId);
                var result = await response.ReadAsString();
                Debug.WriteLine("ProcessRequest({0}) Response Text: '{1}'", requestId, result);
                return result;
            }

            [Fact]
            public async Task SendToClientFromOutsideOfConnection()
            {
                using (var host = new MemoryHost())
                {
                    IPersistentConnectionContext connectionContext = null;
                    host.Configure(app =>
                    {
                        var configuration = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        app.MapSignalR<BroadcastConnection>("/echo", configuration);
                        connectionContext = configuration.Resolver.Resolve<IConnectionManager>().GetConnectionContext<BroadcastConnection>();
                    });

                    var connection1 = new Client.Connection("http://foo/echo");

                    using (connection1)
                    {
                        var wh1 = new AsyncManualResetEvent(initialState: false);

                        await connection1.Start(host);

                        connection1.Received += data =>
                        {
                            Assert.Equal("yay", data);
                            wh1.Set();
                        };

                        var ignore = connectionContext.Connection.Send(connection1.ConnectionId, "yay");

                        Assert.True(await wh1.WaitAsync(TimeSpan.FromSeconds(10)));
                    }
                }
            }

            [Fact]
            public async Task SendToClientsFromOutsideOfConnection()
            {
                using (var host = new MemoryHost())
                {
                    IPersistentConnectionContext connectionContext = null;
                    host.Configure(app =>
                    {
                        var configuration = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        app.MapSignalR<BroadcastConnection>("/echo", configuration);
                        connectionContext = configuration.Resolver.Resolve<IConnectionManager>().GetConnectionContext<BroadcastConnection>();
                    });

                    var connection1 = new Client.Connection("http://foo/echo");

                    using (connection1)
                    {
                        var wh1 = new AsyncManualResetEvent(initialState: false);

                        await connection1.Start(host);

                        connection1.Received += data =>
                        {
                            Assert.Equal("yay", data);
                            wh1.Set();
                        };

                        var ignore = connectionContext.Connection.Send(new[] { connection1.ConnectionId }, "yay");

                        Assert.True(await wh1.WaitAsync(TimeSpan.FromSeconds(10)));
                    }
                }
            }
            
            [Fact]
            public async Task SendToGroupFromOutsideOfConnection()
            {
                using (var host = new MemoryHost())
                {
                    IPersistentConnectionContext connectionContext = null;
                    host.Configure(app =>
                    {
                        var configuration = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        app.MapSignalR<BroadcastConnection>("/echo", configuration);
                        connectionContext = configuration.Resolver.Resolve<IConnectionManager>().GetConnectionContext<BroadcastConnection>();
                    });

                    var connection1 = new Client.Connection("http://foo/echo");

                    using (connection1)
                    {
                        var wh1 = new AsyncManualResetEvent(initialState: false);

                        await connection1.Start(host);

                        connection1.Received += data =>
                        {
                            Assert.Equal("yay", data);
                            wh1.Set();
                        };

                        await connectionContext.Groups.Add(connection1.ConnectionId, "Foo");                        
                        await connectionContext.Groups.Send("Foo", "yay");
                        Assert.True(await wh1.WaitAsync(TimeSpan.FromSeconds(10)));
                    }
                }
            }

            [Fact]
            public async Task SendToGroupsFromOutsideOfConnection()
            {
                using (var host = new MemoryHost())
                {
                    IPersistentConnectionContext connectionContext = null;
                    host.Configure(app =>
                    {
                        var configuration = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        app.MapSignalR<BroadcastConnection>("/echo", configuration);
                        connectionContext = configuration.Resolver.Resolve<IConnectionManager>().GetConnectionContext<BroadcastConnection>();
                    });

                    var connection1 = new Client.Connection("http://foo/echo");

                    using (connection1)
                    {
                        var wh1 = new AsyncManualResetEvent(initialState: false);

                        await connection1.Start(host);

                        connection1.Received += data =>
                        {
                            Assert.Equal("yay", data);
                            wh1.Set();
                        };

                        connectionContext.Groups.Add(connection1.ConnectionId, "Foo").Wait();
                        var ignore = connectionContext.Groups.Send(new[] { "Foo", "Bar" }, "yay");

                        Assert.True(await wh1.WaitAsync(TimeSpan.FromSeconds(10)));
                    }
                }
            }

            [Theory]
            [InlineData(HostType.IISExpress, TransportType.Websockets)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling)]
            [InlineData(HostType.HttpListener, TransportType.Websockets)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling)]
            public async Task BasicAuthCredentialsFlow(HostType hostType, TransportType transportType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize();

                    var connection = CreateConnection(host, "/basicauth/echo");
                    var tcs = new TaskCompletionSource<string>();
                    var mre = new AsyncManualResetEvent();

                    using (connection)
                    {
                        connection.Credentials = new System.Net.NetworkCredential("user", "password");

                        connection.Received += data =>
                        {
                            tcs.TrySetResult(data);
                            mre.Set();
                        };

                        await connection.Start(host.Transport);

                        connection.SendWithTimeout("Hello World");

                        Assert.True(await mre.WaitAsync(TimeSpan.FromSeconds(10)));
                        Assert.Equal("Hello World", tcs.Task.Result);
                    }
                }
            }

            [Theory]
            [InlineData(HostType.Memory, TransportType.Auto, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.Auto, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.Auto, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.Auto, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.Auto, MessageBusType.Default)]
            public void UnableToConnectToProtectedConnection(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    var wh = new AsyncManualResetEvent();
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/protected");

                    using (connection)
                    {
                        Assert.Throws<AggregateException>(() => connection.Start(host.Transport).Wait());
                    }
                }
            }

            [Theory]
            [InlineData(HostType.Memory, TransportType.Auto, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.Auto, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.Auto, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            // [InlineData(HostType.Memory, TransportType.LongPolling)]
            // [InlineData(HostType.IISExpress, TransportType.Auto)]
            public async Task GroupCanBeAddedAndMessagedOnConnected(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    var wh = new AsyncManualResetEvent();
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/add-group");

                    using (connection)
                    {
                        connection.Received += data =>
                        {
                            Assert.Equal("hey", data);
                            wh.Set();
                        };

                        await connection.Start(host.Transport);
                        connection.SendWithTimeout("");

                        Assert.True(await wh.WaitAsync(TimeSpan.FromSeconds(5)));
                    }
                }
            }

            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling, MessageBusType.Default)]
            public async Task SendRaisesOnReceivedFromAllEvents(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/multisend");
                    var results = new List<string>();
                    connection.Received += data =>
                    {
                        results.Add(data);
                    };

                    await connection.Start(host.Transport);
                    connection.SendWithTimeout("");

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    connection.Stop();

                    Debug.WriteLine(String.Join(", ", results));

                    Assert.Equal(4, results.Count);
                    Assert.Equal("OnConnectedAsync1", results[0]);
                    Assert.Equal("OnConnectedAsync2", results[1]);
                    Assert.Equal("OnReceivedAsync1", results[2]);
                    Assert.Equal("OnReceivedAsync2", results[3]);
                }
            }

            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling, MessageBusType.Default)]
            public async Task SendCanBeCalledAfterStateChangedEvent(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/multisend");
                    var results = new List<string>();
                    connection.Received += data =>
                    {
                        results.Add(data);
                    };

                    connection.StateChanged += stateChange =>
                    {
                        if (stateChange.NewState == Client.ConnectionState.Connected)
                        {
                            connection.SendWithTimeout("");
                        }
                    };

                    await connection.Start(host.Transport);

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    connection.Stop();

                    Debug.WriteLine(String.Join(", ", results));

                    Assert.Equal(4, results.Count);
                }
            }
        }

        public class OnReconnectedAsync : HostedTest
        {
            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            // [InlineData(HostType.IISExpress, TransportType.LongPolling)]
            public async Task ReconnectFiresAfterHostShutDown(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/my-reconnect");

                    using (connection)
                    {
                        await connection.Start(host.Transport);

                        host.Shutdown();

                        await Task.Delay(TimeSpan.FromSeconds(5));

                        Assert.Equal(Client.ConnectionState.Reconnecting, connection.State);
                    }
                }
            }

            [Theory]
            [InlineData(TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            public async Task ReconnectDoesntFireAfterTimeOut(TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = new MemoryHost())
                {
                    var conn = new MyReconnect();
                    host.Configure(app =>
                    {
                        var config = new ConnectionConfiguration
                        {
                            Resolver = new DefaultDependencyResolver()
                        };

                        UseMessageBus(messageBusType, config.Resolver);

                        app.MapSignalR<MyReconnect>("/endpoint", config);
                        var configuration = config.Resolver.Resolve<IConfigurationManager>();
                        configuration.DisconnectTimeout = TimeSpan.FromSeconds(6);
                        configuration.ConnectionTimeout = TimeSpan.FromSeconds(2);
                        configuration.KeepAlive = null;

                        config.Resolver.Register(typeof(MyReconnect), () => conn);
                    });

                    var connection = new Client.Connection("http://foo/endpoint");
                    var transport = CreateTransport(transportType, host);
                    await connection.Start(transport);

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    connection.Stop();

                    Assert.Equal(0, conn.Reconnects);
                }
            }
        }

        public class GroupTest : HostedTest
        {
            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            // [InlineData(HostType.IISExpress, TransportType.Websockets)]
            public async Task GroupsReceiveMessages(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/groups");
                    var list = new List<string>();
                    connection.Received += data =>
                    {
                        list.Add(data);
                    };

                    await connection.Start(host.Transport);

                    // Join the group
                    connection.SendWithTimeout(new { type = 1, group = "test" });

                    // Sent a message
                    connection.SendWithTimeout(new { type = 3, group = "test", message = "hello to group test" });

                    // Leave the group
                    connection.SendWithTimeout(new { type = 2, group = "test" });

                    for (int i = 0; i < 10; i++)
                    {
                        // Send a message
                        connection.SendWithTimeout(new { type = 3, group = "test", message = "goodbye to group test" });
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    connection.Stop();

                    Assert.Equal(1, list.Count);
                    Assert.Equal("hello to group test", list[0]);
                }
            }

            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            // [InlineData(HostType.IISExpress, TransportType.Websockets)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling, MessageBusType.Default)]
            public async Task GroupsRejoinedWhenOnRejoiningGroupsOverridden(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(keepAlive: null,
                                    disconnectTimeout: 6,
                                    connectionTimeout: 2,
                                    messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/rejoin-groups");

                    var list = new List<string>();
                    connection.Received += data =>
                    {
                        list.Add(data);
                    };

                    await connection.Start(host.Transport);

                    // Join the group
                    connection.SendWithTimeout(new { type = 1, group = "test" });

                    // Sent a message
                    connection.SendWithTimeout(new { type = 3, group = "test", message = "hello to group test" });

                    // Force Reconnect
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    // Send a message
                    connection.SendWithTimeout(new { type = 3, group = "test", message = "goodbye to group test" });

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    connection.Stop();

                    Assert.Equal(2, list.Count);
                    Assert.Equal("hello to group test", list[0]);
                    Assert.Equal("goodbye to group test", list[1]);
                }
            }
        }

        public class SendFacts : HostedTest
        {
            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling, MessageBusType.Default)]
            public async Task SendToAllButCaller(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection1 = CreateConnection(host, "/filter");
                    var connection2 = CreateConnection(host, "/filter");

                    using (connection1)
                    using (connection2)
                    {
                        var wh1 = new AsyncManualResetEvent(initialState: false);
                        var wh2 = new AsyncManualResetEvent(initialState: false);

                        connection1.Received += data => wh1.Set();
                        connection2.Received += data => wh2.Set();

                        await connection1.Start(host.TransportFactory());
                        await connection2.Start(host.TransportFactory());

                        connection1.SendWithTimeout("test");

                        Assert.False(await wh1.WaitAsync(TimeSpan.FromSeconds(5)));
                        Assert.True(await wh2.WaitAsync(TimeSpan.FromSeconds(5)));
                    }
                }
            }

            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling, MessageBusType.Default)]
            public async Task SendWithSyncErrorThrows(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/sync-error");

                    using (connection)
                    {
                        await connection.Start(host.Transport);

                        Assert.Throws<AggregateException>(() => connection.SendWithTimeout("test"));
                    }
                }
            }
        }

        public class ReceiveFacts : HostedTest
        {
            [Theory]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.ServerSentEvents, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.Fake)]
            [InlineData(HostType.Memory, TransportType.LongPolling, MessageBusType.FakeMultiStream)]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.IISExpress, TransportType.Websockets, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling, MessageBusType.Default)]
            [InlineData(HostType.HttpListener, TransportType.Websockets, MessageBusType.Default)]
            public async Task ReceivePreserializedJson(HostType hostType, TransportType transportType, MessageBusType messageBusType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize(messageBusType: messageBusType);

                    var connection = CreateConnection(host, "/preserialize");
                    var tcs = new TaskCompletionSource<string>();
                    var mre = new AsyncManualResetEvent();

                    connection.Received += json =>
                    {
                        tcs.TrySetResult(json);
                        mre.Set();
                    };

                    using (connection)
                    {
                        await connection.Start(host.Transport);

                        connection.SendWithTimeout(new { preserialized = true });

                        Assert.True(await mre.WaitAsync(TimeSpan.FromSeconds(5)));
                        var json = JObject.Parse(tcs.Task.Result);
                        Assert.True((bool)json["preserialized"]);
                    }
                }
            }
        }

        public class Owin : HostedTest
        {
            [Theory]
            [InlineData(HostType.IISExpress, TransportType.ServerSentEvents)]
            [InlineData(HostType.IISExpress, TransportType.LongPolling)]
            [InlineData(HostType.HttpListener, TransportType.ServerSentEvents)]
            [InlineData(HostType.HttpListener, TransportType.LongPolling)]
            public async Task EnvironmentIsAvailable(HostType hostType, TransportType transportType)
            {
                using (var host = CreateHost(hostType, transportType))
                {
                    host.Initialize();

                    var connection = CreateConnection(host, "/items");
                    var connection2 = CreateConnection(host, "/items");

                    var results = new List<RequestItemsResponse>();
                    connection2.Received += data =>
                    {
                        var val = JsonConvert.DeserializeObject<RequestItemsResponse>(data);
                        if (!results.Contains(val))
                        {
                            results.Add(val);
                        }
                    };

                    await connection.Start(host.TransportFactory());
                    await connection2.Start(host.TransportFactory());

                    await Task.Delay(TimeSpan.FromSeconds(2));

                    connection.SendWithTimeout(null);

                    await Task.Delay(TimeSpan.FromSeconds(2));

                    connection.Stop();

                    await Task.Delay(TimeSpan.FromSeconds(2));

                    Debug.WriteLine(String.Join(", ", results));

                    Assert.Equal(3, results.Count);
                    Assert.Equal("OnConnectedAsync", results[0].Method);
                    Assert.NotNull(results[0].Headers);
                    Assert.NotNull(results[0].Query);
                    Assert.True(results[0].Headers.Count > 0);
                    Assert.True(results[0].Query.Count > 0);
                    Assert.True(results[0].OwinKeys.Length > 0);
                    Assert.Equal("OnReceivedAsync", results[1].Method);
                    Assert.NotNull(results[1].Headers);
                    Assert.NotNull(results[1].Query);
                    Assert.True(results[1].Headers.Count > 0);
                    Assert.True(results[1].Query.Count > 0);
                    Assert.True(results[1].OwinKeys.Length > 0);
                    Assert.Equal("OnDisconnectAsync", results[2].Method);
                    Assert.NotNull(results[2].Headers);
                    Assert.NotNull(results[2].Query);
                    Assert.True(results[2].Headers.Count > 0);
                    Assert.True(results[2].Query.Count > 0);
                    Assert.True(results[2].OwinKeys.Length > 0);

                    connection2.Stop();
                }
            }
        }
    }
}
