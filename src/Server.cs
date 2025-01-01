using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO.Compression;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

var fileDirectory = Directory.GetCurrentDirectory();
for (int i = 0; i < args.Length; i++) {
  var arg = args[i];
  if (arg == "--directory") {
    fileDirectory = args[i + 1];
  }
}

// Uncomment this block to pass the first stage
// Created a TCP Listener, which is a server that uses TCP/IP Stack to establish a connection and send data.
 TcpListener server = new TcpListener(IPAddress.Any, 4221);
 server.Start();
//Client getting connected
while (true)
{
    // chnaging the socket approach to tcp client
    TcpClient client = await server.AcceptTcpClientAsync();  
    Console.WriteLine("Client Connected");
    _ = Task.Run(() =>HandleTcpConnections(client));

}

Task HandleTcpConnections(TcpClient client)
{
    NetworkStream stream = client.GetStream();  

    // recieved the request from the client, storing the stream into a byte array called buffer
    var buffer = new byte[1024];
    int bytesRead = stream.Read(buffer, 0, buffer.Length);


    //As headers, request line and body are spearated by \r\n 
    string request = ASCIIEncoding.UTF8.GetString(buffer, 0 , bytesRead);
    Console.WriteLine("request is : " + request);
    var lines = request.Split("\r\n");
    string[] startLineParts = lines[0].Split(' '); // Lines[0] contains request info called request line
    // From lines[1] to .. it contains headers and body
    // Separate method path and httpverb
    Console.WriteLine("debug point 1");

    // finding the encoding
    string? encoding = null;
    string[]? encodings = null;
    foreach (string line in lines)
    {
        if (line.StartsWith("Accept-Encoding:"))
        {
            encodings = line.Split(" ");
            foreach (string v in encodings)
            {
                string temp = v;
                if (v.ToLower().Trim().EndsWith(","))
                    temp = v.Remove(v.Length - 1);
                if (temp.ToLower().Trim() == "gzip")
                    encoding = temp.ToLower().Trim();
            }
        }
    }

    var (method, path, httpVerbBody) = (startLineParts[0], startLineParts[1], startLineParts[2]);
    // lines[0] = "GET /apple HTTP/1.1"

    // Generating response
    string? response = null;
    byte[] responseBytes = [];
    if(path == "/")
    {
        response = "HTTP/1.1 200 OK\r\n\r\n";
    }
    else if(path.StartsWith("/echo/"))
    {
        byte[] compressedResponse = [];
        if (encoding == "gzip")
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(startLineParts[1].Substring(6));
            using (var outputStream = new MemoryStream())
            {
                using (var gZipStream =
                           new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gZipStream.Write(messageBytes, 0, messageBytes.Length);
                }
                compressedResponse = outputStream.ToArray();
            }
        }
        else
        {
            compressedResponse = Encoding.UTF8.GetBytes(startLineParts[1].Substring(6));
        }
        encoding = encoding != null ? $"\r\nContent-Encoding: {encoding}" : "";
        var compressedMessage =
        $"{httpVerbBody} 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {compressedResponse?.Length}{encoding}\r\n\r\n";
        Console.WriteLine("compressed response : " + compressedResponse);
        responseBytes = [..Encoding.UTF8.GetBytes(compressedMessage), ..compressedResponse];
        //response = compressedMessage + compressedResponse;
        //encoding = encoding != null && encoding != "invalid-encoding"? $"\r\nContent-Encoding: {encoding}" : "";
        //string message = path.Substring(6);
        //response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {message.Length}{encoding}\r\n\r\n{message}";
    }
    else if(path.StartsWith("/user-agent"))
    {
        string userAgent = lines[2].Split(' ')[1];
        response = $"HTTP/1.1 200 OK\r\n" + $"Content-Type: text/plain\r\n" + $"Content-Length: {userAgent.Length}\r\n\r\n" + $"{userAgent}";
    }
    else if(path.StartsWith("/files") && method == "GET")
    {
        //var argv = Environment.GetCommandLineArgs();
        var fileName = path.Split('/')[2];
        var currentDirectory = Environment.GetCommandLineArgs()[2];
        Console.WriteLine("currDir : " + currentDirectory);
        var filePath = currentDirectory + fileName;
        Console.WriteLine("file path to find : " + filePath);
        if (File.Exists(filePath)) {
          var fileContent = File.ReadAllText(filePath);
          response = $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {fileContent.Length}\r\n\r\n{fileContent}";
        } else {
          response = "HTTP/1.1 404 Not Found\r\n\r\n";
        }
    }
    else if (path.StartsWith("/files") && method == "POST")
    {
        var fileName = path.Split('/')[2];
        var filePath = fileDirectory + fileName;
        string fileContent  = lines.Last();
        File.WriteAllText(filePath, fileContent);
        response = "HTTP/1.1 201 Created\r\n\r\n";
    }
    else
    {
        response = "HTTP/1.1 404 Not Found\r\n\r\n";
    }
    Console.WriteLine("response is : " + response);


    // Encoding and sending back the response
    if(response!=null)
    {
        responseBytes = Encoding.ASCII.GetBytes(response);
    }                           
    stream.Write(responseBytes,0, responseBytes.Length);

    return Task.CompletedTask;
}


