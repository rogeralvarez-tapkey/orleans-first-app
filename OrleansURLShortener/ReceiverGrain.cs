using Orleans.Runtime;
using Orleans.Streams;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OrleansURLShortener
{

    public interface IReceiverGrain : IGrainWithGuidKey
    {
        Task HandleMessage(int number);
    }

    [ImplicitStreamSubscription("RANDOMDATA")]
    public class ReceiverGrain : Grain, IReceiverGrain
    {
        private IAsyncStream<int> _stream = null!;

        public Task HandleMessage(int number)
        {
            
            Console.WriteLine("Message received!");
            Console.WriteLine(number);

            if(number%2 is 0)
            {
                throw new Exception("Test Exception");
            }
            return Task.CompletedTask;

        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var streamProvider = this.GetStreamProvider("StreamProvider");


            var streamId = StreamId.Create(
                "RANDOMDATA", this.GetPrimaryKeyString());

            _stream = streamProvider.GetStream<int>(
                streamId);
            // Set our OnNext method to the lambda which simply prints the data.
            // This doesn't make new subscriptions, because we are using implicit 
            // subscriptions via [ImplicitStreamSubscription].
            await _stream.SubscribeAsync<int>(
                async (data, token) =>
                {
                    this.HandleMessage(data);
                    await Task.CompletedTask;
                });

            


            await base.OnActivateAsync(cancellationToken);
        }

    }
}
