using System.Threading.Channels;

namespace ECommerceApp.Application.Messaging
{
    public interface IMessageChannel
    {
        ChannelReader<IMessage> Reader { get; }
        ChannelWriter<IMessage> Writer { get; }
    }
}
