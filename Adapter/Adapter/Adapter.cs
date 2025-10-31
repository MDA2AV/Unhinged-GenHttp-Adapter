using Adapter.Server;
using Adapter.Types;
using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using Unhinged;

namespace Adapter;

public static class Adapter
{
    public static UnhingedEngine.UnhingedBuilder Map(
        this UnhingedEngine.UnhingedBuilder builder, 
        IHandlerBuilder handlerBuilder, 
        IServerCompanion? companion = null)
    {
        builder.InjectRequestHandler(RequestHandler(handlerBuilder.Build(), companion));
        return builder;
    }

    private static async ValueTask GenHttpStaticHandler(Connection connection, IHandler handler, IServerCompanion? companion)
    {
        var server = new ImplicitServer(handler, companion);

        var registeredPath = "/plaintext";

        try
        {
            using var request = new Request(server, connection);
            
            using var response = await handler.HandleAsync(request);

            if (response != null)
            {
                MapResponse(response, connection);

                server.Companion?.OnRequestHandled(request, response);
            }
        }
        catch (Exception e)
        {
            // todo: cannot tell the IP of the client in unhinged
            server.Companion?.OnServerError(ServerErrorScope.ServerConnection, null, e);
            throw;
        }
    }

    private static void MapResponse(IResponse response, Connection connection)
    {
        connection.WriteBuffer.WriteUnmanaged("HTTP/1.1 200 OK\r\n"u8 +
                                              "Server: W\r\n"u8 +
                                              "Content-Type: text/plain\r\n"u8 +
                                              //"Content-Length: 13\r\n\r\nHello, World!"u8);
                                              "Content-Length: 13\r\n"u8);
        connection.WriteBuffer.WriteUnmanaged(DateHelper.HeaderBytes);
        connection.WriteBuffer.WriteUnmanaged("Hello, World!"u8);
    }

    private static void AdvanceTo(Request request, string registeredPath)
    {
        var parts = registeredPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var _ in parts)
        {
            request.Target.Advance();
        }
    }
    
    private static Action<Connection> RequestHandler(IHandler handler, IServerCompanion? companion) =>
        conn => GenHttpStaticHandler(conn, handler, companion);
}