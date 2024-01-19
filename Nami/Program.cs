using System.Net.Sockets;
using System.Net;
using System.Xml.Serialization;
using System.Text;
using System.Web;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Mime;
using System.IO.Pipes;
using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Threading;
using System.Collections;
using System.Data;

namespace Nami
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                if (args[0] == "Help")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Шаблон конфигурирования программы с помощью аргументов командной строки:");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Nami Address=<IP адрес> Port=<Порт> Root=<Корневой каталог>");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Пример:");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Nami Address=127.0.0.1 Port=80 Root=\"C:/Root\"");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Маршруты прописываются только в конфигурационном файле.");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Pattern — шаблон, которому должен соответствовать URL запроса.");
                    Console.WriteLine("Type — тип маршрута:");
                    Console.WriteLine("\tLocal — возвращает статичный файл, указанный в Source, либо возвращает статичный файл, указанный в URL, если Source не указан;");
                    Console.WriteLine("\tRemote — передает запрос на другой сервер, указанный в Source.");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Пример конфигурационного файла:");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(
@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Configuration>
	<Address>127.0.0.1</Address>
	<Port>80</Port>
	<Root>C:/Root</Root>
	<Routes>
		<Route>
			<Pattern>^/$</Pattern>
			<Type>Local</Type>
			<Source>/index.html</Source>
		</Route>
		<Route>
			<Pattern>^/api</Pattern>
			<Type>Remote</Type>
			<Source>127.0.0.1:8080</Source>
		</Route>
		<Route>
			<Pattern>\.html|\.css|\.js$</Pattern>
			<Type>Local</Type>
			<Source></Source>
		</Route>
	</Routes>
</Configuration>");

                    Close();
                    return;
                }
            }

            Configuration? configuration = LoadConfiguration();

            if (configuration == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Не удалось загрузить конфигурацию");

                Close();
                return;
            }

            if (!LoadArguments(args, configuration))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Не удалось загрузить аргументы командной строки");

                Close();
                return;
            }

            if (!CheckConfiguration(configuration))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Конфигурация не прошла валидацию");

                Close();
                return;
            }

            StartServerAsync(configuration);

            Close();
        }

        static void Close()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Для остановки нажмите любую клавишу");
            Console.ReadKey();

            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();
        }

        static Configuration? LoadConfiguration()
        {
            using FileStream fileStream = new FileStream("Configuration.xml", FileMode.Open);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Configuration));
            return xmlSerializer.Deserialize(fileStream) as Configuration;
        }

        static bool LoadArguments(string[] args, Configuration configuration)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith("Address="))
                {
                    string value = arg.Replace("Address=", "");

                    if (value == "")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("В качестве адреса передано пустое значение");
                        return false;
                    }

                    try
                    {
                        IPAddress.Parse(value);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Введен некорректный адрес");
                        return false;
                    }

                    configuration.Address = value;

                    continue;
                }

                if (arg.StartsWith("Port="))
                {
                    string value = arg.Replace("Port=", "");

                    if (value == "")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("В качестве порта передано пустое значение");
                        return false;
                    }

                    int port;

                    try
                    {
                        port = Convert.ToInt16(value);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Введен некорректный порт");
                        return false;
                    }

                    configuration.Port = port;

                    continue;
                }

                if (arg.StartsWith("Root="))
                {
                    string value = arg.Replace("Root=", "");

                    if (Directory.Exists(value))
                    {
                        configuration.Root = value;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Введен несуществующий корневой каталог");
                        return false;
                    }

                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Введен несуществующий параметр");
                return false;
            }

            return true;
        }

        static bool CheckConfiguration(Configuration configuration)
        {
            try
            {
                IPAddress.Parse(configuration.Address);
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Введен некорректный адрес");
                return false;
            }

            try
            {
                Convert.ToInt16(configuration.Port);
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Введен некорректный порт");
                return false;
            }

            if (!Directory.Exists(configuration.Root))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Введен несуществующий корневой каталог");
                return false;
            }

            for (int i = 0; i < configuration.Routes.Count; i++)
            {
                if (configuration.Routes[i].Type != "Local" && configuration.Routes[i].Type != "Remote")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Маршрут №{i + 1} имеет недопустимый тип");
                    return false;
                }

                if (configuration.Routes[i].Type == "Remote" && configuration.Routes[i].Source == "")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Маршрут №{i + 1} обязан содержать ссылку на другой сервер");
                    return false;
                }
            }

            return true;
        }

        static async void StartServerAsync(Configuration configuration)
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Parse(configuration.Address), configuration.Port);
                listener.Start();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Сервер запущен");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Адрес: {configuration.Address}");
                Console.WriteLine($"Порт: {configuration.Port}");
                Console.WriteLine($"Корневой каталог: {configuration.Root}");

                SemaphoreSlim semaphore = new SemaphoreSlim(1024);

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    HandleClientAsync(client, semaphore, configuration);
                }
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.Message);
            }
        }

        static async void HandleClientAsync(TcpClient client,  SemaphoreSlim semaphore, Configuration configuration)
        {
            try
            {
                int limit = 10;
                int count = 0;
                int timeout = 1;

                while (client.Connected && count < limit)
                {
                    count++;

                    NetworkStream networkStream = client.GetStream();
                    byte[] request;
                    Task<byte[]> readLineAsync = ReadLineAsync(networkStream);

                    if (await Task.WhenAny(readLineAsync, Task.Delay(timeout * 1000)) == readLineAsync)
                    {
                        request = readLineAsync.Result;
                    }
                    else
                    {
                        break;
                    }

                    string fullRequest = Encoding.UTF8.GetString(request);

                    await HandleRequestAsync(request, fullRequest, networkStream, timeout, count, limit, client, semaphore, configuration);
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exception.Message);
                    client.Close();
                }
            }
            finally
            {
                client.Close();
            }
        }

        static async Task HandleRequestAsync(byte[] request, string fullRequest, NetworkStream networkStream, int timeout, int count, int limit, TcpClient client, SemaphoreSlim semaphore, Configuration configuration)
        {
            try
            {
                string[] requestArgs = Encoding.UTF8.GetString(request).Split(" ");
                string method = requestArgs[0];
                string? url = HttpUtility.UrlDecode(requestArgs[1]);

                bool notFound = true;

                foreach (Route route in configuration.Routes)
                {
                    Regex regex = new Regex(route.Pattern);

                    if (!regex.IsMatch(url))
                    {
                        continue;
                    }

                    notFound = false;

                    if (route.Type == "Local")
                    {
                        if (method != "GET")
                        {
                            await Send405Async(networkStream, client);
                            break;
                        }

                        if (route.Source == string.Empty)
                        {
                            await HandleLocalRouteAsync(configuration.Root + url, networkStream, timeout, count, limit, client, fullRequest, route);
                        }
                        else
                        {
                            await HandleLocalRouteAsync(configuration.Root + route.Source, networkStream, timeout, count, limit, client, fullRequest, route);
                        }
                    }

                    if (route.Type == "Remote")
                    {
                        await HandleRemoteRouteAsync(route.Source, request, networkStream, client, semaphore, fullRequest, route);
                    }
                }

                if (notFound)
                {
                    await Send404Async(networkStream, client);
                }
            }
            catch
            {
                await Send400Async(networkStream, client);
            }
        }

        static async Task HandleLocalRouteAsync(string fileName, NetworkStream networkStream, int timeout, int count, int limit, TcpClient client, string fullRequest, Route route)
        {
            try
            {
                //Проверка файла на существование
                if (!File.Exists(fileName))
                {
                    await Send404Async(networkStream, client);
                    return;
                }

                //Выбор типа контента в зависимости от расширения файла
                string? extension = Path.GetExtension(fileName);

                string? contentType;
                ContentTypes.TryGetValue(extension, out contentType);
                if (contentType == null)
                {
                    contentType = "application/octet-stream";
                }

                //Чтение заголовков и открытие файлового потока
                Dictionary<string, string> requestHeaders = await GetHeadersAsync(networkStream);

                using FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

                //В зависимости от разных условий выбираем стратегию отправки ответа
                if (fileStream.Length == long.MaxValue)
                {
                    await SendChunksAsync(contentType, fileStream, networkStream, timeout, count, limit, client, fullRequest, route);
                }
                else if (requestHeaders.ContainsKey("Range"))
                {
                    await SendRangesAsync(contentType, requestHeaders, fileStream, networkStream, timeout, count, limit, client, fullRequest, route);
                }
                else
                {
                    await SendFileAsync(contentType, fileStream, networkStream, timeout, count, limit, client, fullRequest, route);
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
        }

        static async Task SendChunksAsync(string contentType, FileStream fileStream, NetworkStream networkStream, int timeout, int count, int limit, TcpClient client, string fullRequest, Route route)
        {
            try
            {
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("HTTP/1.1 200 OK"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("Accept-Ranges: none"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Type: {contentType}"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("Transfer-Encoding: chunked"));
                if (count == 1)
                {
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Keep-Alive: timeout={timeout}, max={limit}"));
                }
                await WriteLineAsync(networkStream, new byte[0]);

                int bytes;
                byte[] buffer = new byte[4096];
                while ((bytes = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes(bytes.ToString("X")));
                    await networkStream.WriteAsync(buffer, 0, bytes);
                    await WriteLineAsync(networkStream, new byte[0]);
                }

                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("0"));
                await WriteLineAsync(networkStream, new byte[0]);
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
        }

        static async Task SendRangesAsync(string contentType, Dictionary<string, string> requestHeaders, FileStream fileStream, NetworkStream networkStream, int timeout, int count, int limit, TcpClient client, string fullRequest, Route route)
        {
            try
            {
                string[] ranges = requestHeaders["Range"].Replace("bytes=", "").Split(", ");

                if (ranges.Length == 1)
                {
                    string[] range = ranges[0].Split("-");

                    long start;
                    long end;
                    long length;

                    CalculateRange(range, fileStream, out start, out end, out length);
                    await SendRangeAsync(contentType, fileStream, networkStream, start, end, length, timeout, count, limit, client, fullRequest, route);
                }
                else
                {
                    long length = CalculateMultipartBody(ranges, fileStream, contentType);

                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("HTTP/1.1 206 Partial Content"));
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("Accept-Ranges: bytes"));
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Length: {length}"));
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("Content-Type: multipart/byteranges; boundary=GRANDLINE"));
                    if (count == 1)
                    {
                        await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Keep-Alive: timeout={timeout}, max={limit}"));
                    }
                    await WriteLineAsync(networkStream, new byte[0]);

                    foreach (string args in ranges)
                    {
                        string[] range = args.Split("-");

                        long start;
                        long end;

                        CalculateRange(range, fileStream, out start, out end, out length);
                        await SendMultipartRangeAsync(contentType, fileStream, networkStream, start, end, length, client, fullRequest, route);
                    }

                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("--GRANDLINE--"));
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
        }

        static void CalculateRange(string[] range, FileStream fileStream, out long start, out long end, out long length)
        {
            if (range[0] != "" && range[1] != "")
            {
                start = long.Parse(range[0]);
                end = long.Parse(range[1]);
                length = end - start + 1;
            }
            else if (range[0] != "")
            {
                start = long.Parse(range[0]);
                end = fileStream.Length - 1;
                length = end - start + 1;
            }
            else
            {
                length = long.Parse(range[1]);
                end = fileStream.Length - 1;
                start = end - length + 1;
            }
        }

        static async Task SendRangeAsync(string contentType, FileStream fileStream, NetworkStream networkStream, long start, long end, long length, int timeout, int count, int limit, TcpClient client, string fullRequest, Route route)
        {
            try
            {
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("HTTP/1.1 206 Partial Content"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("Accept-Ranges: bytes"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Type: {contentType}"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Range: bytes {start}-{end}/{fileStream.Length}"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Length: {length}"));
                if (count == 1)
                {
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Keep-Alive: timeout={timeout}, max={limit}"));
                }
                await WriteLineAsync(networkStream, new byte[0]);

                int bytes;
                byte[] buffer = new byte[4096];
                fileStream.Seek(start, SeekOrigin.Begin);
                while ((bytes = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (length < bytes)
                    {
                        bytes = (int)length;
                    }
                    else
                    {
                        length -= bytes;
                    }

                    await networkStream.WriteAsync(buffer, 0, bytes);

                    if (length == 0)
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
        }

        static long CalculateMultipartBody(string[] ranges, FileStream fileStream, string contentType)
        {
            long result = 0;

            foreach (string args in ranges)
            {
                result += Encoding.UTF8.GetBytes("--GRANDLINE").Length + 2;
                result += Encoding.UTF8.GetBytes($"Content-Type: {contentType}").Length + 2;

                long start;
                long end;
                long length;

                string[] range = args.Split("-");

                CalculateRange(range, fileStream, out start, out end, out length);

                result += Encoding.UTF8.GetBytes($"Content-Range: bytes {start}-{end}/{fileStream.Length}").Length + 2;
                result += 2;

                result += length;
            }

            result += Encoding.UTF8.GetBytes("--GRANDLINE--").Length + 2;

            return result;
        }

        static async Task SendMultipartRangeAsync(string contentType, FileStream fileStream, NetworkStream networkStream, long start, long end, long length, TcpClient client, string fullRequest, Route route)
        {
            try
            {
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("--GRANDLINE"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Type: {contentType}"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Range: bytes {start}-{end}/{fileStream.Length}"));
                await WriteLineAsync(networkStream, new byte[0]);

                int bytes;
                byte[] buffer = new byte[4096];
                fileStream.Seek(start, SeekOrigin.Begin);
                while ((bytes = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (length < bytes)
                    {
                        bytes = (int)length;
                    }
                    else
                    {
                        length -= bytes;
                    }

                    await networkStream.WriteAsync(buffer, 0, bytes);

                    if (length == 0)
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
        }

        static async Task SendFileAsync(string contentType, FileStream fileStream, NetworkStream networkStream, int timeout, int count, int limit, TcpClient client, string fullRequest, Route route)
        {
            try
            {
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("HTTP/1.1 200 OK"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes("Accept-Ranges: bytes"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Type: {contentType}"));
                await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Content-Length: {fileStream.Length}"));
                if (count == 1)
                {
                    await WriteLineAsync(networkStream, Encoding.UTF8.GetBytes($"Keep-Alive: timeout={timeout}, max={limit}"));
                }
                await WriteLineAsync(networkStream, new byte[0]);

                int bytes;
                byte[] buffer = new byte[4096];
                while ((bytes = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await networkStream.WriteAsync(buffer, 0, bytes);
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
        }

        static async Task HandleRemoteRouteAsync(string socket, byte[] request, Stream clientNetworkStream, TcpClient client, SemaphoreSlim semaphore, string fullRequest, Route route)
        {
            try
            {
                //Дожидаемся свободного потока
                await semaphore.WaitAsync();

                //Инициализируем прокси для связи с другим сервером
                using TcpClient proxy = new TcpClient();
                string[] args = socket.Split(":");
                await proxy.ConnectAsync(IPAddress.Parse(args[0]), Convert.ToInt16(args[1]));
                NetworkStream proxyNetworkStream = proxy.GetStream();

                //Посылаем серверу данные запроса, пришедшие от клиента
                await WriteLineAsync(proxyNetworkStream, request);

                Dictionary<string, string> requestHeaders = await GetHeadersAsync(clientNetworkStream);

                if (requestHeaders.ContainsKey("Connection"))
                {
                    requestHeaders["Connection"] = "close";
                }
                else
                {
                    requestHeaders.Add("Connection", "close");
                }

                foreach (string key in requestHeaders.Keys)
                {
                    await WriteLineAsync(proxyNetworkStream, Encoding.UTF8.GetBytes($"{key}: {requestHeaders[key]}"));
                }
                await WriteLineAsync(proxyNetworkStream, new byte[0]);

                //Если передана длина тела запроса, читаем тело и отправляем на другой сервер
                if (requestHeaders.ContainsKey("Content-Length"))
                {
                    long length = long.Parse(requestHeaders["Content-Length"]);

                    int count;
                    byte[] buffer = new byte[2048];
                    do
                    {
                        count = await clientNetworkStream.ReadAsync(buffer, 0, buffer.Length);
                        await proxyNetworkStream.WriteAsync(buffer, 0, count);
                        length -= count;
                    } while (length > 0);
                }

                //Отправка ответа от сервера клиенту
                byte[] response = await ReadLineAsync(proxyNetworkStream);
                await WriteLineAsync(clientNetworkStream, response);

                Dictionary<string, string> responseHeaders = await GetHeadersAsync(proxyNetworkStream);

                if (responseHeaders.ContainsKey("Connection"))
                {
                    responseHeaders["Connection"] = "keep-alive";
                }
                else
                {
                    responseHeaders.Add("Connection", "keep-alive");
                }

                foreach (string key in responseHeaders.Keys)
                {
                    await WriteLineAsync(clientNetworkStream, Encoding.UTF8.GetBytes($"{key}: {responseHeaders[key]}"));
                }
                await WriteLineAsync(clientNetworkStream, new byte[0]);

                //Если в заголовках есть Transfer-Encoding, то читаем чанки
                if (responseHeaders.ContainsKey("Transfer-Encoding"))
                {
                    byte[] chunkSizeLine;
                    long chunkSize;

                    while ((chunkSizeLine = await ReadLineAsync(proxyNetworkStream)).Length != 0)
                    {
                        await WriteLineAsync(clientNetworkStream, chunkSizeLine);
                        chunkSize = long.Parse(Encoding.UTF8.GetString(chunkSizeLine), System.Globalization.NumberStyles.HexNumber);
                        long length = chunkSize;

                        int count;
                        byte[] buffer = new byte[4096];

                        do
                        {
                            if (length < 4096)
                            {
                                buffer = new byte[length];
                            }

                            count = await proxyNetworkStream.ReadAsync(buffer, 0, buffer.Length);
                            await clientNetworkStream.WriteAsync(buffer, 0, count);
                            length -= count;
                        } while (length > 0);

                        await ReadLineAsync(proxyNetworkStream);
                        await WriteLineAsync(clientNetworkStream, new byte[0]);
                    }
                }
                //Если в заголовках указана длина тела, то читаем в соответствии с ней
                else if (responseHeaders.ContainsKey("Content-Length"))
                {
                    long length = long.Parse(responseHeaders["Content-Length"]);

                    int count;
                    byte[] buffer = new byte[4096];
                    do
                    {
                        if (length < 4096)
                        {
                            buffer = new byte[length];
                        }

                        count = await proxyNetworkStream.ReadAsync(buffer, 0, buffer.Length);
                        await clientNetworkStream.WriteAsync(buffer, 0, count);
                        length -= count;
                    } while (length > 0);
                }
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Запрос: {fullRequest}\nШаблон маршрута: {route.Pattern}\nТип маршрута: {route.Type}\nИсточник: {route.Source}\nОшибка: {exception.Message}");
                    client.Close();
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        static async Task Send400Async(Stream stream, TcpClient client)
        {
            try
            {
                await WriteLineAsync(stream, Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request"));
                await WriteLineAsync(stream, new byte[0]);
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exception.Message);
                    client.Close();
                }
            }
        }

        static async Task Send404Async(Stream stream, TcpClient client)
        {
            try
            {
                await WriteLineAsync(stream, Encoding.UTF8.GetBytes("HTTP/1.1 404 Not Found"));
                await WriteLineAsync(stream, new byte[0]);
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exception.Message);
                    client.Close();
                }
            }
        }

        static async Task Send405Async(Stream stream, TcpClient client)
        {
            try
            {
                await WriteLineAsync(stream, Encoding.UTF8.GetBytes("HTTP/1.1 405 Method Not Allowed"));
                await WriteLineAsync(stream, Encoding.UTF8.GetBytes("Allowed: GET"));
                await WriteLineAsync(stream, new byte[0]);
            }
            catch (Exception exception)
            {
                if (client.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exception.Message);
                    client.Close();
                }
            }
        }

        static Dictionary<string, string> ContentTypes = new Dictionary<string, string>
        {
            { ".txt", "text/plain" },
            { ".html", "text/html" },
            { ".css", "text/css" },
            { ".js", "text/javascript" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".svg", "image/svg+xml" },
            { ".webp", "image/webp" },
            { ".tiff", "image/tiff" },
            { ".ico", "image/vnd.microsoft.icon" },
            { ".gif", "image/gif" },
            { ".mp3", "audio/mpeg" },
            { ".mp4", "video/mp4" }
        };

        static async Task<byte[]> ReadLineAsync(Stream stream)
        {
            List<byte> result = new List<byte>();

            byte[] value = new byte[1];
            while (await stream.ReadAsync(value, 0, 1) > 0)
            {
                if (value[0] == (byte)'\n')
                {
                    break;
                }
                else
                {
                    result.Add(value[0]);
                }
            }

            if (result.Count > 0)
            {
                if (result[result.Count - 1] == (byte)'\r')
                {
                    result.RemoveAt(result.Count - 1);
                }
            }

            return result.ToArray();
        }

        static async Task WriteLineAsync(Stream stream, byte[] line)
        {
            byte[] buffer = new byte[line.Length + 2];
            line.CopyTo(buffer, 0);
            buffer[buffer.Length - 2] = (byte)'\r';
            buffer[buffer.Length - 1] = (byte)'\n';

            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        static async Task<Dictionary<string, string>> GetHeadersAsync(Stream stream)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            byte[] line;
            while ((line = await ReadLineAsync(stream)).Length != 0)
            {
                string[] args = Encoding.UTF8.GetString(line).Split(": ");
                result.Add(args[0], args[1]);
            }

            return result;
        }
    }
}