// <Configuration>
using Microsoft.AspNetCore.Http.Extensions;
using Orleans.Runtime;
using Orleans.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Storage;
using System.Text;
using Orleans.Hosting;

var builder = WebApplication.CreateBuilder();
var serviceCollection = new ServiceCollection()
    .AddSerializer()
    .BuildServiceProvider();



var serializer = new OrleansGrainStorageSerializer(serviceCollection.GetRequiredService<Serializer>());



builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage("PubSub");
    siloBuilder.UseDashboard(x => x.HostSelf = true);


    // siloBuilder.AddAzureTableGrainStorage(
    //         name: "urls",
    //         configureOptions: options =>
    //         {
    //             // Configure the storage connection key
    //             options.ConfigureTableServiceClient(
    //                 "UseDevelopmentStorage=true");
    //         });
    siloBuilder.AddAdoNetGrainStorage("urls", options =>
       {
           options.Invariant = "System.Data.SqlClient";
           options.ConnectionString = "Data Source=(local);Integrated Security=true;Initial Catalog=orleansdemo";
           //options.UseJsonFormat = true;

       });

    // siloBuilder.Services.AddSerializer(serializerBuilder =>
    // {
    //    serializerBuilder.AddJsonSerializer(
    //        isSupported: type => type.Namespace.StartsWith("Orleans"));
    // });


    // Streams Configuration

    siloBuilder.AddMemoryStreams("StreamProvider")
    .AddMemoryGrainStorage("PubSubStore");

});
// </Configuration>

var app = builder.Build();
// <Endpoints>

app.Map("/dashboard", x => x.UseOrleansDashboard());
app.MapGet("/", () =>
{
    var result = serializer.Serialize(new UrlDetails() { ShortenedRouteSegment = "123", FullUrl = "test" });

    var response = Encoding.ASCII.GetString(result);

    var deserialized = serializer.Deserialize<UrlDetails>(result);
    return Results.Ok(response);
});

app.MapGet("/shorten",
    async (IGrainFactory grains, HttpRequest request, string redirect) =>
    {
        // Create a unique, short ID
        var shortenedRouteSegment = Guid.NewGuid().GetHashCode().ToString("X");

        // Create and persist a grain with the shortened ID and full URL
        var shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment);
        await shortenerGrain.SetUrl(redirect);

        // Return the shortened URL for later use
        var resultBuilder = new UriBuilder($"{request.Scheme}://{request.Host.Value}")
        {
            Path = $"/go/{shortenedRouteSegment}"
        };

        return Results.Ok(resultBuilder.Uri);
    });

    app.MapPost("/stream",
    async (IGrainFactory grains, HttpRequest request, string redirect) =>
    {
       //// Pick a GUID for a chat room grain and chat room stream
       // var guid = new Guid("some guid identifying the chat room");
       // // Get one of the providers which we defined in our config
       // var streamProvider = client.Stre  GetStreamProvider("StreamProvider");
       // // Get the reference to a stream
       // var streamId = StreamId.Create("RANDOMDATA", guid);
       // var stream = streamProvider.GetStream<int>(streamId);

       // await stream.OnNextAsync(2);

        return Results.Ok("asd");
    });

app.MapGet("/go/{shortenedRouteSegment}",
    async (IGrainFactory grains, string shortenedRouteSegment) =>
    {
        // Retrieve the grain using the shortened ID and redirect to the original URL        
        var shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment);
        var url = await shortenerGrain.GetUrl();

        return Results.Redirect(url);
    });

app.Run();
// </Endpoints>

// <GrainInterface>
public interface IUrlShortenerGrain : IGrainWithStringKey
{
    Task SetUrl(string fullUrl);
    Task<string> GetUrl();
}
// </GrainInterface>

// <Grain>
public class UrlShortenerGrain : Grain, IUrlShortenerGrain
{
    private readonly IPersistentState<UrlDetails> _state;

    public UrlShortenerGrain(
        [PersistentState(
                stateName: "url",
                storageName: "urls")]
                IPersistentState<UrlDetails> state)
    {
        _state = state;
    }

    public async Task SetUrl(string fullUrl)
    {
        _state.State = new UrlDetails() { ShortenedRouteSegment = this.GetPrimaryKeyString(), FullUrl = fullUrl };
        await _state.WriteStateAsync();
    }

    public Task<string> GetUrl()
    {
        return Task.FromResult(_state.State.FullUrl);
    }
}

[GenerateSerializer()]
[Alias("UrlDetails")]
public class UrlDetails
{
    [Id(0)]

    public string FullUrl { get; set; }
    [Id(1)]
    public string ShortenedRouteSegment { get; set; }

}
// </Grain>