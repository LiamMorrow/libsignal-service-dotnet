using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using libsignal;
using libsignal.messages.multidevice;
using libsignal.state;
using libsignal_service_dotnet.messages.calls;
using libsignalmetadatadotnet;
using libsignalservice.messages.multidevice;
using libsignalservice.messages.shared;
using libsignalservice.push;
using libsignalservice.serialize;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using serialize.protos;

namespace libsignalservice.messages
{
    public class SignalServiceContent
    {
        private static readonly ILogger logger = LibsignalLogging.CreateLogger<SignalServiceContent>();
        public SignalServiceAddress Sender { get; }
        public int SenderDevice { get; }
        public long Timestamp { get; }
        public bool NeedsReceipt { get; }
        private readonly SignalServiceContentProto serializedState;

        public SignalServiceDataMessage? Message { get; }
        public SignalServiceSyncMessage? SynchronizeMessage { get; }
        public SignalServiceCallMessage? CallingMessage { get; }
        public SignalServiceReceiptMessage? ReadMessage { get; }
        public SignalServiceTypingMessage? TypingMessage { get; }

        private SignalServiceContent(SignalServiceDataMessage message, SignalServiceAddress sender, int senderDevice, long timestamp, bool needsReceipt, SignalServiceContentProto serializedState)
        {
            Sender = sender;
            SenderDevice = senderDevice;
            Timestamp = timestamp;
            NeedsReceipt = needsReceipt;
            this.serializedState = serializedState;

            Message = message;
            SynchronizeMessage = null;
            CallingMessage = null;
            ReadMessage = null;
            TypingMessage = null;
        }

        private SignalServiceContent(SignalServiceSyncMessage synchronizeMessage, SignalServiceAddress sender, int senderDevice, long timestamp, bool needsReceipt, SignalServiceContentProto serializedState)
        {
            Sender = sender;
            SenderDevice = senderDevice;
            Timestamp = timestamp;
            NeedsReceipt = needsReceipt;
            this.serializedState = serializedState;

            Message = null;
            SynchronizeMessage = synchronizeMessage;
            CallingMessage = null;
            ReadMessage = null;
            TypingMessage = null;
        }

        private SignalServiceContent(SignalServiceCallMessage callMessage, SignalServiceAddress sender, int senderDevice, long timestamp, bool needsReceipt, SignalServiceContentProto serializedState)
        {
            Sender = sender;
            SenderDevice = senderDevice;
            Timestamp = timestamp;
            NeedsReceipt = needsReceipt;
            this.serializedState = serializedState;

            Message = null;
            SynchronizeMessage = null;
            CallingMessage = callMessage;
            ReadMessage = null;
            TypingMessage = null;
        }

        private SignalServiceContent(SignalServiceReceiptMessage receiptMessage, SignalServiceAddress sender, int senderDevice, long timestamp, bool needsReceipt, SignalServiceContentProto serializedState)
        {
            Sender = sender;
            SenderDevice = senderDevice;
            Timestamp = timestamp;
            NeedsReceipt = needsReceipt;
            this.serializedState = serializedState;

            Message = null;
            SynchronizeMessage = null;
            CallingMessage = null;
            ReadMessage = receiptMessage;
            TypingMessage = null;
        }

        private SignalServiceContent(SignalServiceTypingMessage typingMessage, SignalServiceAddress sender, int senderDevice, long timestamp, bool needsReceipt, SignalServiceContentProto serializedState)
        {
            Sender = sender;
            SenderDevice = senderDevice;
            Timestamp = timestamp;
            NeedsReceipt = needsReceipt;
            this.serializedState = serializedState;

            Message = null;
            SynchronizeMessage = null;
            CallingMessage = null;
            ReadMessage = null;
            TypingMessage = typingMessage;
        }

        public byte[] Serialize()
        {
            return serializedState.ToByteArray();
        }

        public static SignalServiceContent? Deserialize(byte[] data)
        {
            try
            {
                if (data == null)
                {
                    return null;
                }

                SignalServiceContentProto signalServiceContentProto = SignalServiceContentProto.Parser.ParseFrom(data);

                return CreateFromProto(signalServiceContentProto);
            }
            catch (Exception ex) when (ex is InvalidProtocolBufferException || ex is ProtocolInvalidMessageException ||
                ex is ProtocolInvalidKeyException)
            {
                // We do not expect any of these exceptions if this byte[] has come from serialize.
                throw new ArgumentException(null, ex);
            }
        }

        /// <summary>
        /// Takes internal protobuf serialization format and processes it into a <see cref="SignalServiceContent"/>.
        /// </summary>
        /// <param name="serviceContentProto"></param>
        /// <returns></returns>
        /// <exception cref="ProtocolInvalidMessageException"></exception>
        /// <exception cref="ProtocolInvalidKeyException"></exception>
        public static SignalServiceContent? CreateFromProto(SignalServiceContentProto serviceContentProto)
        {
            SignalServiceMetadata metadata = SignalServiceMetadataProtobufSerializer.FromProtobuf(serviceContentProto.Metadata);
            SignalServiceAddress localAddress = SignalServiceAddressProtobufSerializer.FromProtobuf(serviceContentProto.LocalAddress);

            if (serviceContentProto.DataCase == SignalServiceContentProto.DataOneofCase.LegacyDataMessage)
            {
                DataMessage message = serviceContentProto.LegacyDataMessage;

                return new SignalServiceContent(CreateSignalServiceMessage(metadata, message),
                    metadata.Sender,
                    metadata.SenderDevice,
                    metadata.Timestamp,
                    metadata.NeedsReceipt,
                    serviceContentProto);
            }
            else if (serviceContentProto.DataCase == SignalServiceContentProto.DataOneofCase.Content)
            {
                Content message = serviceContentProto.Content;

                if (message.DataMessage != null)
                {
                    return new SignalServiceContent(CreateSignalServiceMessage(metadata, message.DataMessage),
                        metadata.Sender,
                        metadata.SenderDevice,
                        metadata.Timestamp,
                        metadata.NeedsReceipt,
                        serviceContentProto);
                }
                else if (message.SyncMessage != null && localAddress.Matches(metadata.Sender))
                {
                    return new SignalServiceContent(CreateSynchronizeMessage(metadata, message.SyncMessage),
                        metadata.Sender,
                        metadata.SenderDevice,
                        metadata.Timestamp,
                        metadata.NeedsReceipt,
                        serviceContentProto);
                }
                else if (message.CallingMessage != null)
                {
                    return new SignalServiceContent(CreateCallingMessage(message.CallingMessage),
                        metadata.Sender,
                        metadata.SenderDevice,
                        metadata.Timestamp,
                        metadata.NeedsReceipt,
                        serviceContentProto);
                }
                else if (message.ReceiptMessage != null)
                {
                    return new SignalServiceContent(CreateReceiptMessage(metadata, message.ReceiptMessage),
                        metadata.Sender,
                        metadata.SenderDevice,
                        metadata.Timestamp,
                        metadata.NeedsReceipt,
                        serviceContentProto);
                }
                else if (message.TypingMessage != null)
                {
                    return new SignalServiceContent(CreateTypingMessage(metadata, message.TypingMessage),
                        metadata.Sender,
                        metadata.SenderDevice,
                        metadata.Timestamp,
                        false,
                        serviceContentProto);
                }
            }

            return null;
        }

        private static SignalServiceDataMessage CreateSignalServiceMessage(SignalServiceMetadata metadata, DataMessage content)
        {
            SignalServiceGroup? groupInfo = CreateGroupV1Info(content);
            List<SignalServiceAttachment> attachments = new List<SignalServiceAttachment>();
            bool endSession = ((content.Flags & (uint)DataMessage.Types.Flags.EndSession) != 0);
            bool expirationUpdate = ((content.Flags & (uint)DataMessage.Types.Flags.ExpirationTimerUpdate) != 0);
            bool profileKeyUpdate = ((content.Flags & (uint)DataMessage.Types.Flags.ProfileKeyUpdate) != 0);
            SignalServiceDataMessage.SignalServiceQuote? quote = CreateQuote(content);
            List<SharedContact>? sharedContacts = CreateSharedContacts(content);
            List<SignalServiceDataMessage.SignalServicePreview>? previews = CreatePreviews(content);
            SignalServiceDataMessage.SignalServiceSticker? sticker = CreateSticker(content);

            if (content.RequiredProtocolVersion > (int)DataMessage.Types.ProtocolVersion.Current)
            {
                throw new UnsupportedDataMessageException((int)DataMessage.Types.ProtocolVersion.Current,
                    (int)content.RequiredProtocolVersion,
                    metadata.Sender.GetIdentifier(),
                    metadata.SenderDevice,
                    groupInfo);
            }

            foreach (AttachmentPointer pointer in content.Attachments)
            {
                attachments.Add(CreateAttachmentPointer(pointer));
            }

            if (content.HasTimestamp && (long)content.Timestamp != metadata.Timestamp)
            {
                throw new ProtocolInvalidMessageException(new InvalidMessageException("Timestamps don't match: " + content.Timestamp + " vs " + metadata.Timestamp),
                                                                           metadata.Sender.GetIdentifier(),
                                                                           metadata.SenderDevice);
            }

            return new SignalServiceDataMessage(metadata.Timestamp,
                groupInfo,
                attachments,
                content.Body,
                endSession,
                (int)content.ExpireTimer,
                expirationUpdate,
                content.HasProfileKey ? content.ProfileKey.ToByteArray() : null,
                profileKeyUpdate,
                quote,
                sharedContacts,
                previews,
                sticker,
                content.IsViewOnce);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <exception cref="ProtocolInvalidMessageException"></exception>
        /// <exception cref="ProtocolInvalidKeyException"></exception>
        private static SignalServiceSyncMessage CreateSynchronizeMessage(SignalServiceMetadata metadata, SyncMessage content)
        {
            if (content.Sent != null)
            {
                var unidentifiedStatuses = new Dictionary<SignalServiceAddress, bool>();
                SyncMessage.Types.Sent sentContent = content.Sent;
                SignalServiceDataMessage dataMessage = CreateSignalServiceMessage(metadata, sentContent.Message);
                SignalServiceAddress? address = SignalServiceAddress.IsValidAddress(sentContent.DestinationUuid, sentContent.Destination) ?
                    new SignalServiceAddress(UuidUtil.ParseOrNull(sentContent.DestinationUuid), sentContent.Destination) :
                    null;

                if (address == null && dataMessage.Group == null)
                {
                    throw new ProtocolInvalidMessageException(new InvalidMessageException("SyncMessage missing both destination and group ID!"), null, 0);
                }

                foreach (var status in sentContent.UnidentifiedStatus)
                {
                    if (SignalServiceAddress.IsValidAddress(status.DestinationUuid, status.Destination))
                    {
                        SignalServiceAddress recipient = new SignalServiceAddress(UuidUtil.ParseOrNull(status.DestinationUuid), status.Destination);
                        unidentifiedStatuses.Add(recipient, status.Unidentified);
                    }
                    else
                    {
                        logger.LogWarning("Encountered an invalid UnidentifiedDeliveryStatus in a SentTranscript! Ignoring.");
                    }
                }

                return SignalServiceSyncMessage.ForSentTranscript(new SentTranscriptMessage(address!,
                    (long)sentContent.Timestamp,
                    CreateSignalServiceMessage(metadata, sentContent.Message),
                    (long)sentContent.ExpirationStartTimestamp,
                    unidentifiedStatuses,
                    sentContent.IsRecipientUpdate));
            }

            if (content.Request != null)
            {
                return SignalServiceSyncMessage.ForRequest(new RequestMessage(content.Request));
            }

            if (content.Read.Count > 0)
            {
                List<ReadMessage> readMessages = new List<ReadMessage>();

                foreach (SyncMessage.Types.Read read in content.Read)
                {
                    if (SignalServiceAddress.IsValidAddress(read.SenderUuid, read.Sender))
                    {
                        SignalServiceAddress address = new SignalServiceAddress(UuidUtil.ParseOrNull(read.SenderUuid), read.Sender);
                        readMessages.Add(new ReadMessage(address, (long)read.Timestamp));
                    }
                    else
                    {
                        logger.LogWarning("Encountered an invalid ReadMessage! Ignoring.");
                    }
                }

                return SignalServiceSyncMessage.ForRead(readMessages);
            }

            if (content.ViewOnceOpen != null)
            {
                if (SignalServiceAddress.IsValidAddress(content.ViewOnceOpen.SenderUuid, content.ViewOnceOpen.Sender))
                {
                    SignalServiceAddress address = new SignalServiceAddress(UuidUtil.ParseOrNull(content.ViewOnceOpen.SenderUuid), content.ViewOnceOpen.Sender);
                    ViewOnceOpenMessage timerRead = new ViewOnceOpenMessage(address, (long)content.ViewOnceOpen.Timestamp);
                    return SignalServiceSyncMessage.ForViewOnceOpen(timerRead);
                }
                else
                {
                    throw new ProtocolInvalidMessageException(new InvalidMessageException("ViewOnceOpen message has no sender!"), null, 0);
                }
            }

            if (content.Contacts != null)
            {
                AttachmentPointer pointer = content.Contacts.Blob;
                return SignalServiceSyncMessage.ForContacts(new ContactsMessage(CreateAttachmentPointer(pointer), content.Contacts.Complete));
            }

            if (content.Groups != null)
            {
                AttachmentPointer pointer = content.Groups.Blob;
                return SignalServiceSyncMessage.ForGroups(CreateAttachmentPointer(pointer));
            }

            if (content.Verified != null)
            {
                if (SignalServiceAddress.IsValidAddress(content.Verified.DestinationUuid, content.Verified.Destination))
                {
                    try
                    {
                        Verified verified = content.Verified;
                        SignalServiceAddress destination = new SignalServiceAddress(UuidUtil.ParseOrNull(verified.DestinationUuid), verified.Destination);
                        IdentityKey identityKey = new IdentityKey(verified.IdentityKey.ToByteArray(), 0);

                        VerifiedMessage.VerifiedState verifiedState;

                        if (verified.State == Verified.Types.State.Default)
                        {
                            verifiedState = VerifiedMessage.VerifiedState.Default;
                        }
                        else if (verified.State == Verified.Types.State.Verified)
                        {
                            verifiedState = VerifiedMessage.VerifiedState.Verified;
                        }
                        else if (verified.State == Verified.Types.State.Unverified)
                        {
                            verifiedState = VerifiedMessage.VerifiedState.Unverified;
                        }
                        else
                        {
                            throw new ProtocolInvalidMessageException(new InvalidMessageException($"Unknown state: {(int)verified.State}"),
                                metadata.Sender.GetIdentifier(), metadata.SenderDevice);
                        }

                        return SignalServiceSyncMessage.ForVerified(new VerifiedMessage(destination, identityKey, verifiedState, Util.CurrentTimeMillis()));
                    }
                    catch (InvalidKeyException ex)
                    {
                        throw new ProtocolInvalidKeyException(ex, metadata.Sender.GetIdentifier(), metadata.SenderDevice);
                    }
                }
                else
                {
                    throw new ProtocolInvalidMessageException(new InvalidMessageException("Verified message has no sender!"), null, 0);
                }
            }

            if (content.StickerPackOperation.Count > 0)
            {
                List<StickerPackOperationMessage> operations = new List<StickerPackOperationMessage>();

                foreach (var operation in content.StickerPackOperation)
                {
                    byte[]? packId = operation.HasPackId ? operation.PackId.ToByteArray() : null;
                    byte[]? packKey = operation.HasPackKey ? operation.PackKey.ToByteArray() : null;
                    StickerPackOperationMessage.OperationType? type = null;

                    if (operation.HasType)
                    {
                        switch (operation.Type)
                        {
                            case SyncMessage.Types.StickerPackOperation.Types.Type.Install: type = StickerPackOperationMessage.OperationType.Install; break;
                            case SyncMessage.Types.StickerPackOperation.Types.Type.Remove: type = StickerPackOperationMessage.OperationType.Remove; break;
                        }
                    }
                    operations.Add(new StickerPackOperationMessage(packId, packKey, type));
                }

                return SignalServiceSyncMessage.ForStickerPackOperations(operations);
            }

            if (content.Blocked != null)
            {
                List<string> numbers = content.Blocked.Numbers.ToList();
                List<string> uuids = content.Blocked.Uuids.ToList();
                List<SignalServiceAddress> addresses = new List<SignalServiceAddress>(numbers.Count + uuids.Count);
                List<byte[]> groupIds = new List<byte[]>(content.Blocked.GroupIds.Count);

                foreach (string e164 in numbers)
                {
                    SignalServiceAddress? address = SignalServiceAddress.FromRaw(null, e164);
                    if (address != null)
                    {
                        addresses.Add(address);
                    }
                }

                foreach (string uuid in uuids)
                {
                    SignalServiceAddress? address = SignalServiceAddress.FromRaw(uuid, null);
                    if (address != null)
                    {
                        addresses.Add(address);
                    }
                }

                foreach (ByteString groupId in content.Blocked.GroupIds)
                {
                    groupIds.Add(groupId.ToByteArray());
                }

                return SignalServiceSyncMessage.ForBlocked(new BlockedListMessage(addresses, groupIds));
            }

            if (content.Configuration != null)
            {
                bool? readReceipts = content.Configuration.HasReadReceipts ? content.Configuration.ReadReceipts : (bool?)null;
                bool? unidentifiedDeliveryIndicators = content.Configuration.HasUnidentifiedDeliveryIndicators ? content.Configuration.UnidentifiedDeliveryIndicators : (bool?)null;
                bool? typingIndicators = content.Configuration.HasTypingIndicators ? content.Configuration.TypingIndicators : (bool?)null;
                bool? linkPreviews = content.Configuration.HasLinkPreviews ? content.Configuration.LinkPreviews : (bool?)null;

                return SignalServiceSyncMessage.ForConfiguration(new ConfigurationMessage(readReceipts, unidentifiedDeliveryIndicators, typingIndicators, linkPreviews));
            }

            return SignalServiceSyncMessage.Empty();
        }

        private static SignalServiceCallMessage CreateCallingMessage(CallingMessage content)
        {
            if (content.Offer != null)
            {
                return new SignalServiceCallMessage()
                {
                    OfferMessage = new OfferMessage()
                    {
                        Id = content.Offer.CallId,
                        Opaque = content.Offer.Opaque.ToByteArray()
                    }
                };
            }
            else if (content.Answer != null)
            {
                return new SignalServiceCallMessage()
                {
                    AnswerMessage = new AnswerMessage()
                    {
                        Id = content.Answer.CallId,
                        Opaque = content.Answer.Opaque.ToByteArray()
                    }
                };
            }
            else if (content.IceCandidates.Count > 0)
            {
                var m = new SignalServiceCallMessage();
                var l = new List<IceUpdateMessage>();
                foreach (var u in content.IceCandidates)
                {
                    l.Add(new IceUpdateMessage()
                    {
                        CallId = u.CallId,
                        Opaque = u.Opaque.ToByteArray()
                    });
                }
                m.IceUpdateMessages = l;
                return m;
            }
            else if (content.Hangup != null)
            {
                return new SignalServiceCallMessage()
                {
                    HangupMessage = new HangupMessage()
                    {
                        Id = content.Hangup.CallId,
                    }
                };
            }
            else if (content.Busy != null)
            {
                return new SignalServiceCallMessage()
                {
                    BusyMessage = new BusyMessage()
                    {
                        Id = content.Busy.CallId
                    }
                };
            }

            return new SignalServiceCallMessage();
        }

        private static SignalServiceReceiptMessage CreateReceiptMessage(SignalServiceMetadata metadata, ReceiptMessage content)
        {
            SignalServiceReceiptMessage.Type type;

            if (content.Type == ReceiptMessage.Types.Type.Delivery)
            {
                type = SignalServiceReceiptMessage.Type.DELIVERY;
            }
            else if (content.Type == ReceiptMessage.Types.Type.Read)
            {
                type = SignalServiceReceiptMessage.Type.READ;
            }
            else
            {
                type = SignalServiceReceiptMessage.Type.UNKNOWN;
            }

            return new SignalServiceReceiptMessage()
            {
                ReceiptType = type,
                Timestamps = content.Timestamp.ToList(),
                When = metadata.Timestamp
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <exception cref="ProtocolInvalidMessageException"></exception>
        private static SignalServiceTypingMessage CreateTypingMessage(SignalServiceMetadata metadata, TypingMessage content)
        {
            SignalServiceTypingMessage.Action action;

            if (content.Action == push.TypingMessage.Types.Action.Started) action = SignalServiceTypingMessage.Action.STARTED;
            else if (content.Action == push.TypingMessage.Types.Action.Stopped) action = SignalServiceTypingMessage.Action.STOPPED;
            else action = SignalServiceTypingMessage.Action.UNKNOWN;

            if (content.HasTimestamp && (long)content.Timestamp != metadata.Timestamp)
            {
                throw new ProtocolInvalidMessageException(new InvalidMessageException($"Timestamps don't match: {content.Timestamp} vs {metadata.Timestamp}"),
                    metadata.Sender.GetIdentifier(),
                    metadata.SenderDevice);
            }

            return new SignalServiceTypingMessage(action, (long)content.Timestamp,
                content.HasGroupId ? content.GroupId.ToByteArray() : null);
        }

        private static SignalServiceDataMessage.SignalServiceQuote? CreateQuote(DataMessage content)
        {
            if (content.Quote == null)
                return null;

            var attachments = new List<SignalServiceDataMessage.SignalServiceQuotedAttachment>();

            foreach (var attachment in content.Quote.Attachments)
            {
                attachments.Add(new SignalServiceDataMessage.SignalServiceQuotedAttachment(attachment.ContentType,
                    attachment.FileName,
                    attachment.Thumbnail != null ? CreateAttachmentPointer(attachment.Thumbnail) : null));
            }

            if (SignalServiceAddress.IsValidAddress(content.Quote.AuthorUuid, content.Quote.Author))
            {
                SignalServiceAddress address = new SignalServiceAddress(UuidUtil.ParseOrNull(content.Quote.AuthorUuid), content.Quote.Author);

                return new SignalServiceDataMessage.SignalServiceQuote((long)content.Quote.Id,
                    address,
                    content.Quote.Text,
                    attachments);
            }
            else
            {
                logger.LogWarning("Quote was missing an author! Returning null.");
                return null;
            }
        }

        private static List<SignalServiceDataMessage.SignalServicePreview>? CreatePreviews(DataMessage content)
        {
            if (content.Preview.Count <= 0) return null;

            List<SignalServiceDataMessage.SignalServicePreview> results = new List<SignalServiceDataMessage.SignalServicePreview>();

            foreach (var preview in content.Preview)
            {
                SignalServiceAttachment? attachment = null;

                if (preview.Image != null)
                {
                    attachment = CreateAttachmentPointer(preview.Image);
                }

                results.Add(new SignalServiceDataMessage.SignalServicePreview(preview.Url,
                    preview.Title,
                    attachment));
            }

            return results;
        }

        private static SignalServiceDataMessage.SignalServiceSticker? CreateSticker(DataMessage content)
        {
            if (content.Sticker == null ||
                !content.Sticker.HasPackId ||
                !content.Sticker.HasPackKey ||
                !content.Sticker.HasStickerId ||
                content.Sticker.Data == null)
            {
                return null;
            }

            DataMessage.Types.Sticker sticker = content.Sticker;

            return new SignalServiceDataMessage.SignalServiceSticker(sticker.PackId.ToByteArray(),
                sticker.PackKey.ToByteArray(),
                (int)sticker.StickerId,
                CreateAttachmentPointer(sticker.Data));
        }

        private static List<SharedContact>? CreateSharedContacts(DataMessage content)
        {
            if (content.Contact.Count <= 0) return null;

            var results = new List<SharedContact>();

            foreach (var contact in content.Contact)
            {
                Name name = new Name(contact.Name.DisplayName,
                    contact.Name.GivenName,
                    contact.Name.FamilyName,
                    contact.Name.Prefix,
                    contact.Name.Suffix,
                    contact.Name.MiddleName);

                List<PostalAddress> postalAddresses = new List<PostalAddress>();
                if (contact.Address.Count > 0)
                {
                    foreach (var address in contact.Address)
                    {
                        PostalAddress.PostalAddressType postalAddressType = PostalAddress.PostalAddressType.HOME;

                        switch (address.Type)
                        {
                            case DataMessage.Types.Contact.Types.PostalAddress.Types.Type.Work: postalAddressType = PostalAddress.PostalAddressType.WORK; break;
                            case DataMessage.Types.Contact.Types.PostalAddress.Types.Type.Home: postalAddressType = PostalAddress.PostalAddressType.HOME; break;
                            case DataMessage.Types.Contact.Types.PostalAddress.Types.Type.Custom: postalAddressType = PostalAddress.PostalAddressType.CUSTOM; break;
                        }

                        postalAddresses.Add(new PostalAddress(postalAddressType,
                            address.Label,
                            address.Street,
                            address.Pobox,
                            address.Neighborhood,
                            address.City,
                            address.Region,
                            address.Postcode,
                            address.Country));
                    }
                }

                List<Phone> phones = new List<Phone>();
                if (contact.Number.Count > 0)
                {
                    foreach (var phone in contact.Number)
                    {
                        Phone.PhoneType phoneType = Phone.PhoneType.HOME;

                        switch (phone.Type)
                        {
                            case DataMessage.Types.Contact.Types.Phone.Types.Type.Home: phoneType = Phone.PhoneType.HOME; break;
                            case DataMessage.Types.Contact.Types.Phone.Types.Type.Work: phoneType = Phone.PhoneType.WORK; break;
                            case DataMessage.Types.Contact.Types.Phone.Types.Type.Mobile: phoneType = Phone.PhoneType.MOBILE; break;
                            case DataMessage.Types.Contact.Types.Phone.Types.Type.Custom: phoneType = Phone.PhoneType.CUSTOM; break;
                        }

                        phones.Add(new Phone(phone.Value, phoneType, phone.Label));
                    }
                }

                List<Email> emails = new List<Email>();
                if (contact.Email.Count > 0)
                {
                    foreach (var email in contact.Email)
                    {
                        Email.EmailType emailType = Email.EmailType.HOME;

                        switch (email.Type)
                        {
                            case DataMessage.Types.Contact.Types.Email.Types.Type.Home: emailType = Email.EmailType.HOME; break;
                            case DataMessage.Types.Contact.Types.Email.Types.Type.Work: emailType = Email.EmailType.WORK; break;
                            case DataMessage.Types.Contact.Types.Email.Types.Type.Mobile: emailType = Email.EmailType.MOBILE; break;
                            case DataMessage.Types.Contact.Types.Email.Types.Type.Custom: emailType = Email.EmailType.CUSTOM; break;
                        }

                        emails.Add(new Email(email.Value, emailType, email.Label));
                    }
                }

                Avatar? avatar = null;
                if (contact.Avatar != null)
                {
                    avatar = new Avatar(CreateAttachmentPointer(contact.Avatar.Avatar_), contact.Avatar.IsProfile);
                }

                string? organization = null;
                if (contact.HasOrganization)
                {
                    organization = contact.Organization;
                }

                SharedContact sharedContact = new SharedContact(name, avatar,
                    phones.Count > 0 ? phones : null,
                    emails.Count > 0 ? emails : null,
                    postalAddresses.Count > 0 ? postalAddresses : null,
                    organization);
                results.Add(sharedContact);
            }

            return results;
        }

        private static SignalServiceAttachmentPointer CreateAttachmentPointer(AttachmentPointer pointer)
        {
            return new SignalServiceAttachmentPointer((int)pointer.CdnNumber,
                SignalServiceAttachmentRemoteId.From(pointer)!,
                pointer.ContentType,
                pointer.Key.ToByteArray(),
                pointer.HasSize ? pointer.Size : (uint?)null,
                pointer.HasThumbnail ? pointer.Thumbnail.ToByteArray() : null,
                (int)pointer.Width,
                (int)pointer.Height,
                pointer.HasDigest ? pointer.Digest.ToByteArray() : null,
                pointer.HasFileName ? pointer.FileName : null,
                (pointer.Flags & (uint)AttachmentPointer.Types.Flags.VoiceMessage) != 0,
                pointer.HasCaption ? pointer.Caption : null,
                pointer.HasBlurHash ? pointer.BlurHash : null,
                pointer.HasUploadTimestamp ? (long)pointer.UploadTimestamp : 0);
        }

        private static SignalServiceGroup? CreateGroupV1Info(DataMessage content)
        {
            if (content.Group == null) return null;

            var type = content.Group.Type switch
            {
                GroupContext.Types.Type.Deliver => SignalServiceGroup.GroupType.DELIVER,
                GroupContext.Types.Type.Update => SignalServiceGroup.GroupType.UPDATE,
                GroupContext.Types.Type.Quit => SignalServiceGroup.GroupType.QUIT,
                GroupContext.Types.Type.RequestInfo => SignalServiceGroup.GroupType.REQUEST_INFO,
                _ => SignalServiceGroup.GroupType.UNKNOWN,
            };

            if (content.Group.Type != GroupContext.Types.Type.Deliver)
            {
                string? name = null;
                List<SignalServiceAddress>? members = null;
                SignalServiceAttachmentPointer? avatar = null;

                if (content.Group.HasName)
                {
                    name = content.Group.Name;
                }

                if (content.Group.MembersE164.Count > 0)
                {
                    members = new List<SignalServiceAddress>(content.Group.MembersE164.Count);

                    foreach (string member in content.Group.MembersE164)
                    {
                        members.Add(new SignalServiceAddress(null, member));
                    }
                }

                if (content.Group.Avatar != null)
                {
                    AttachmentPointer pointer = content.Group.Avatar;

                    avatar = new SignalServiceAttachmentPointer((int)pointer.CdnNumber,
                        SignalServiceAttachmentRemoteId.From(pointer)!,
                        pointer.ContentType,
                        pointer.Key.ToByteArray(),
                        pointer.HasSize ? pointer.Size : 0,
                        null,
                        0, 0,
                        pointer.HasDigest ? pointer.Digest.ToByteArray() : null,
                        null,
                        false,
                        null,
                        null,
                        pointer.HasUploadTimestamp ? (long)pointer.UploadTimestamp : 0);
                }

                return new SignalServiceGroup(type, content.Group.Id.ToByteArray(), name, members, avatar);
            }

            return new SignalServiceGroup(content.Group.Id.ToByteArray());
        }
    }
}
