namespace libsignal_service_dotnet.messages.calls
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class IceUpdateMessage
    {
        public ulong CallId { get; set; }

        public byte[] Opaque { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
