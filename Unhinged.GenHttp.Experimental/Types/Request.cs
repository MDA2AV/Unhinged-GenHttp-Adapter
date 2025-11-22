using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Api.Routing;
using GenHTTP.Engine.Shared.Types;

using Unhinged.GenHttp.Experimental.Server;

namespace Unhinged.GenHttp.Experimental.Types;

public sealed class Request : IRequest
{
    private readonly ResponseBuilder _responseBuilder;

    private bool _freshResponse = true;

    private IServer? _server;

    private IClientConnection? _client;
    private IClientConnection? _localCLient;

    private FlexibleRequestMethod? _method;
    private RoutingTarget? _target;

    private readonly RequestProperties _properties = new();

    private readonly Query _query = new();

    private readonly CookieCollection _cookies = new();

    private readonly ForwardingCollection _forwardings = new();

    private readonly Headers _headers = new();

    #region Get-/Setters

    public IRequestProperties Properties => _properties;

    public IServer Server => _server ?? throw new InvalidOperationException("Request is not initialized yet");

    public IEndPoint EndPoint => throw new InvalidOperationException("EndPoint is not available as it is managed by Unhinged");

    public IClientConnection Client => _client ?? throw new InvalidOperationException("Request is not initialized yet");

    public IClientConnection LocalClient => _localCLient ?? throw new InvalidOperationException("Request is not initialized yet");

    public HttpProtocol ProtocolType { get; private set; }

    public FlexibleRequestMethod Method => _method ?? throw new InvalidOperationException("Request is not initialized yet");

    public RoutingTarget Target => _target?? throw new InvalidOperationException("Request is not initialized yet");

    public string? UserAgent => this["User-Agent"];

    public string? Referer => this["Referer"];

    public string? Host => this["Host"];

    public string? this[string additionalHeader] => Headers.GetValueOrDefault(additionalHeader);

    public IRequestQuery Query => _query;

    public ICookieCollection Cookies => _cookies;

    public IForwardingCollection Forwardings => _forwardings;

    public IHeaderCollection Headers => _headers;

    // TODO: Wrap the request content received by the client
    // For now there is never content, Unhinged doesn't yet support requests with body
    public Stream Content => Stream.Null;

    public FlexibleContentType? ContentType
    {
        get
        {
            if (Headers.TryGetValue("Content-Type", out var contentType))
            {
                return FlexibleContentType.Parse(contentType);
            }

            // TODO: Unhinged does not support requests with body yet
            return null;
        }
    }

    private Connection? Connection { get; set; }

    #endregion

    #region Initialization

    public Request(ResponseBuilder responseBuilder)
    {
        _responseBuilder = responseBuilder;
    }

    #endregion

    #region Functionality

    public IResponseBuilder Respond()
    {
        if (!_freshResponse)
        {
            _responseBuilder.Reset();
        }
        else
        {
            _freshResponse = false;
        }

        return _responseBuilder;
    }

    public UpgradeInfo Upgrade() => throw new NotSupportedException("Web sockets are not supported by Unhinged");

    public void Configure(ImplicitServer server, Connection connection)
    {
        _server = server;

        Connection = connection;

        // todo: Unhinged only supports Http11
        ProtocolType = HttpProtocol.Http11;

        _method = FlexibleRequestMethod.Get(connection.H1HeaderData.HttpMethod);
        _target = new RoutingTarget(WebPath.FromString(connection.H1HeaderData.Route));

        _headers.SetConnection(connection);
        _query.SetConnection(connection);

        if (connection.H1HeaderData.Headers.TryGetValue("forwarded", out var entry))
        {
            _forwardings.Add(entry);
        }
        else
        {
            _forwardings.TryAddLegacy(Headers);
        }

        _localCLient = new ClientConnection(connection);

        // todo: potential client certificate is not exposed by unhinged
        // Unhinged does not support Tls
        _client = _forwardings.DetermineClient(null) ?? LocalClient;
    }

    private CookieCollection FetchCookies(Connection connection)
    {
        // TODO: Get cookies from the connection
        var cookies = new CookieCollection();

        if (connection.H1HeaderData.Headers.TryGetValue("Cookie", out var header))
        {
            cookies.Add(header);
        }

        return cookies;
    }

    internal void Reset()
    {
        _headers.SetConnection(null);
        _query.SetConnection(null);

        _cookies.Clear();
        _forwardings.Clear();
        _properties.Clear();
        
        _server = null;
        _client = null;
        _localCLient = null;
        _method = null;
        
        _freshResponse = true;
    }

    #endregion

    #region Lifecycle

    public void Dispose()
    {
        // nop
    }

    #endregion

}
