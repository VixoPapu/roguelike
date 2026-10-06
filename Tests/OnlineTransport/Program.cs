using System.Net; using System.Net.Sockets; using ProjectLike.Online;
static void Check(bool okay,string message){if(!okay)throw new Exception(message);Console.WriteLine("PASS "+message);}
static async Task<string> Receive(DirectConnection c){var until=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<until){if(c.TryReceive(out var value))return value;await Task.Delay(5);}throw new Exception("receive timeout: "+c.Error);}
var text="{\"kind\":\"hello\",\"text\":\"Espada el?ctrica, energ?a y escudo\"}";
Check(DirectConnection.Decode(DirectConnection.Encode(text))==text,"compressed UTF8 roundtrip");
var listen=new TcpListener(IPAddress.Loopback,0);listen.Start();int port=((IPEndPoint)listen.LocalEndpoint).Port;
using var sock1=new TcpClient();await sock1.ConnectAsync(IPAddress.Loopback,port);using var host1=new DirectConnection(await listen.AcceptTcpClientAsync());using var peer1=new DirectConnection(sock1);
using var sock2=new TcpClient();await sock2.ConnectAsync(IPAddress.Loopback,port);using var host2=new DirectConnection(await listen.AcceptTcpClientAsync());using var peer2=new DirectConnection(sock2);
peer1.Send("player2");peer2.Send("player3");Check(await Receive(host1)=="player2"&&await Receive(host2)=="player3","independent peers and frame boundaries");
for(int i=0;i<100;i++)host1.Send("reliable"+i);for(int i=0;i<100;i++)if(await Receive(peer1)!="reliable"+i)throw new Exception("out of order");Check(true,"100 reliable frames in order");
for(int i=0;i<1000;i++)host2.Send("{\"kind\":\"state\",\"index\":"+i+"}",true);
await Task.Delay(100);string latest="";while(peer2.TryReceive(out var frame))latest=frame;Check(latest.Contains("999"),"latest snapshot replaces backlog");
bool rejected=false;try{DirectConnection.Encode(new string('x',DirectConnection.MaximumFrame+1));}catch(InvalidDataException){rejected=true;}Check(rejected,"oversized decompressed frame rejected");
peer1.Dispose();await Task.Delay(100);Check(!host1.Connected,"remote disconnect detected");
using var malformed=new TcpClient();await malformed.ConnectAsync(IPAddress.Loopback,port);using var reject=new DirectConnection(await listen.AcceptTcpClientAsync());await malformed.GetStream().WriteAsync(new byte[]{255,255,255,127});await Task.Delay(100);Check(!reject.Connected,"invalid declared length disconnects");listen.Stop();Console.WriteLine("ALL TRANSPORT TESTS PASSED");