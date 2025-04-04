﻿using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

// StudentNumbers: 0864154, 0907615
// StudentNames: Elder dos Santos, Yasin Mesdar

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    private static Socket? _udpSocket;
    private static EndPoint? _serverEndPoint;

    //TODO: [Deserialize Setting.json]
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static void start()
    {
        try
        {
            Initialize();
            SendHello();
            ReceiveWelcome();
            ProcessAllDnsLookups();
            ReceiveEnd();
        }
        catch (Exception e)
        {
            HandleError(e);
        }
        finally
        {
            _udpSocket?.Dispose();
        }
    }

    //TODO: [Create endpoints and socket]
    private void Initialize()
    {
        _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(setting!.ServerIPAddress!), setting.ServerPortNumber);
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress!), setting.ClientPortNumber);
        _udpSocket.Bind(clientEndPoint);

        Logging(" Socket initialized and bound to client endpoint.");
    }

    //TODO: [Create and send HELLO]
    private static void SendHello()
    {
        var message = new Message
        {
            MsgId = 1,
            MsgType = MessageType.Hello,
            Content = "Hello from client"
        };

        SendMessage(message);
        Logging($"Client: Sent -> Type: {message.MsgType}, content: {message.Content}");
    }

    //TODO: [Receive and print Welcome from server]
    private static void ReceiveWelcome()
    {
        var message = ReceiveMessage();
        if (message.MsgType == MessageType.Welcome)
        {
            Logging($"Client: Received -> Type: {message.MsgType}, content: {message.Content}");
        }
        else
        {
            Logging($"Client: Received unexpected message, expected a welcome message -> Type: {message.MsgType}, content: {message.Content}");
        }
    }

    // TODO: [Create and send DNSLookup Message]    
    private static void SendDnsLookupMessage(Message dnsMessage)
    {
        SendMessage(dnsMessage);
        Logging($"Client: Sent {dnsMessage.MsgType} -> MsgId: {dnsMessage.MsgId}");
    }

    //TODO: [Receive and handle reply with Ack]
    private static void ReceiveAndHandleMessage(int expectedMsgId)
    {
        var message = ReceiveMessage();

        if (message.MsgType == MessageType.Ack)
        {
            if (int.TryParse(message.Content?.ToString(), out int ackId))
            {
                Logging($"Client: Received valid Ack for MsgId {ackId}");
            }
            else
            {
                Logging("Client: Received Ack with invalid content.");
            }
        }

        switch (message.MsgType)
        {
            case MessageType.DNSLookupReply:
                Logging($"Client: Received -> DNSLookupReply, MsgId: {message.MsgId}, Content: {message.Content}");
                break;

            case MessageType.Error:
                Logging($"Client: Received -> Error, MsgId: {message.MsgId}, Content: {message.Content}");
                break;

            case MessageType.End:
                Logging("Client: Received End -> closing connection.");
                break;

            default:
                throw new MessageTypeMismatchException($"Unexpected message type received: {message.MsgType}");
        }

        if (message.MsgType != MessageType.End)
        {
            SendAck(expectedMsgId);
        }
    }


    //TODO: [Send Acknowledgment to Server]
    private static void SendAck(int originalMsgId)
    {
        var ack = new Message
        {
            MsgId = originalMsgId + 1000,
            MsgType = MessageType.Ack,
            Content = originalMsgId
        };

        SendMessage(ack);
        Logging($"Client: Sent Ack -> for MsgId: {originalMsgId}");
    }

    // TODO: [Send next DNSLookup to server]
    private static void ProcessAllDnsLookups()
    {
        var lookups = CreateDnsLookupMessages();

        foreach (var msg in lookups)
        {
            SendDnsLookupMessage(msg);
            ReceiveAndHandleMessage(msg.MsgId);
        }
    }

    // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

    //TODO: [Receive and print End from server]
    private static void ReceiveEnd()
    {
        var end = ReceiveMessage();
        if (end.MsgType == MessageType.End)
        {
            Logging("Client: Received End -> closing client.");
        }
        else
        {
            Logging($"Client: Expected End but got {end.MsgType}");
        }
    }

    private static List<Message> CreateDnsLookupMessages()
    {
        return new List<Message>
            {
                new Message
                {
                    MsgId = 101,
                    MsgType = MessageType.DNSLookup,
                    Content = new DNSRecord { Type = "A", Name = "www.test.com" }
                },
                new Message
                {
                    MsgId = 102,
                    MsgType = MessageType.DNSLookup,
                    Content = new DNSRecord { Type = "MX", Name = "example.com" }
                },
                new Message
                {
                    MsgId = 103,
                    MsgType = MessageType.DNSLookup,
                    Content = "unknown.domain"
                },
                new Message
                {
                    MsgId = 104,
                    MsgType = MessageType.DNSLookup,
                    Content = new { Type = "A", Value = "invalid.com" }
                }
            };
    }

    private static string SerializeMessage(Message message)
    {
        return JsonSerializer.Serialize(message);
    }

    private static void SendMessage(Message message)
    {
        var data = Encoding.UTF8.GetBytes(SerializeMessage(message));
        _udpSocket!.SendTo(data, _serverEndPoint!);
    }

    private static Message ReceiveMessage()
    {
        try
        {
            var buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = _udpSocket!.ReceiveFrom(buffer, ref remoteEP);
            var jsonString = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            var message = DeserializeMessage(jsonString);

            if (message.MsgType == MessageType.Error)
            {
                throw new ServerResponseException($"Received Error message: {message.Content}");
            }

            return message;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            throw new TimeoutException("Timeout: Did not receive message from server in time.");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.MessageSize)
        {
            throw new InvalidOperationException("Message too large for buffer.");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            throw new InvalidOperationException("Server not available or forcibly closed the connection.");
        }
    }

    private static Message DeserializeMessage(string data)
    {
        var message = JsonSerializer.Deserialize<Message>(data);
        if (message == null)
        {
            throw new JsonException("Deserialization failed. Received an invalid message format.");
        }
        return message;
    }

    private static void Logging(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
        else
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message is empty!");
        }
    }

    private static void HandleError(Exception ex)
    {
        Logging($"Client: Error {ex.GetType().Name} -> {ex.Message}");

        try
        {
            var errorMsg = new Message
            {
                MsgId = 9999,
                MsgType = MessageType.Error,
                Content = ex.Message
            };
            SendMessage(errorMsg);
        }
        catch
        {
            Logging("Client: Failed to send error message to server.");
        }       
    }
}

public class MessageTypeMismatchException : Exception
{
    public MessageTypeMismatchException(string message) : base(message) { }
}

public class ServerResponseException : Exception
{
    public ServerResponseException(string message) : base(message) { }
}
