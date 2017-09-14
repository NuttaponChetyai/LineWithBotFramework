﻿using LineMessagingAPISDK;
using LineMessagingAPISDK.Models;
using LineWithBotFrameworkApplication2.Services;
using Microsoft.Bot.Connector.DirectLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using dl = Microsoft.Bot.Connector.DirectLine;
using lm = LineMessagingAPISDK.Models;
using LineWithBotFrameworkApplication2.Models.Fttx;
using LineWithBotFrameworkApplication2.Models.InternetAccount;
using LineWithBotFrameworkApplication2.Models;
using System.Data.Entity;

namespace LineWithBotFrameworkApplication2.Controllers
{
    public class LineMessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post(HttpRequestMessage request)
        {
            if (!await VaridateSignature(request))
                return Request.CreateResponse(HttpStatusCode.BadRequest);

            lm.Activity activity = JsonConvert.DeserializeObject<lm.Activity>
                (await request.Content.ReadAsStringAsync());

            // Line may send multiple events in one message, so need to handle them all.
            foreach (Event lineEvent in activity.Events)
            {
                LineMessageHandler handler = new LineMessageHandler(lineEvent);
                await handler.Initialize();
                Profile profile = await handler.GetProfile(lineEvent.Source.UserId);

                switch (lineEvent.Type)
                {
                    case EventType.Beacon:
                        await handler.HandleBeaconEvent();
                        break;
                    case EventType.Follow:
                        await handler.HandleFollowEvent();
                        break;
                    case EventType.Join:
                        await handler.HandleJoinEvent();
                        break;
                    case EventType.Leave:
                        await handler.HandleLeaveEvent();
                        break;
                    case EventType.Message:
                        Message message = JsonConvert.DeserializeObject<Message>(lineEvent.Message.ToString());
                        switch (message.Type)
                        {
                            case MessageType.Text:
                                await handler.HandleTextMessage();
                                break;
                            case MessageType.Audio:
                            case MessageType.Image:
                            case MessageType.Video:
                                await handler.HandleMediaMessage();
                                break;
                            case MessageType.Sticker:
                                await handler.HandleStickerMessage();
                                break;
                            case MessageType.Location:
                                await handler.HandleLocationMessage();
                                break;
                        }
                        break;
                    case EventType.Postback:
                        await handler.HandlePostbackEvent();
                        break;
                    case EventType.Unfollow:
                        await handler.HandleUnfollowEvent();
                        break;
                }
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task<bool> VaridateSignature(HttpRequestMessage request)
        {
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ConfigurationManager.AppSettings["ChannelSecret"].ToString()));
            var computeHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(await request.Content.ReadAsStringAsync()));
            var contentHash = Convert.ToBase64String(computeHash);
            var headerHash = Request.Headers.GetValues("X-Line-Signature").First();

            return contentHash == headerHash;
        }
    }

    public class LineMessageHandler
    {
        private Event lineEvent;
        private static string directLineSecret = ConfigurationManager.AppSettings["DirectLineSecret"].ToString();
        private LineClient lineClient = new LineClient(ConfigurationManager.AppSettings["ChannelToken"].ToString());
        private DirectLineClient dlClient = new DirectLineClient(directLineSecret);
        private string conversationId; // DirectLine ConversationId
        private string watermark; // Limit the messages to get from DirectLine
        private Dictionary<string, object> userParams;

        public LineMessageHandler(Event lineEvent)
        {
            this.lineEvent = lineEvent;
        }

        public async Task Initialize()
        {
            var lineId = lineEvent.Source.UserId ?? lineEvent.Source.GroupId ?? lineEvent.Source.RoomId;

            //if (CacheService.caches.Keys.Contains(lineId))
            //{
            //    // Get preserved ConversationId and Watermark from cache.
            //    // If we scale out, then we have to use different method
            //    userParams = CacheService.caches[lineId] as Dictionary<string, object>;
            //    conversationId = userParams.Keys.Contains("ConversationId") ? userParams["ConversationId"].ToString() : "";
            //    watermark = userParams.Keys.Contains("Watermark") ? userParams["Watermark"].ToString() : null;
            //}
            //else
            //{
                // If no cache, then create new one.
                userParams = new Dictionary<string, object>();
                var conversation = await dlClient.Conversations.StartConversationAsync();
                userParams["ConversationId"] = conversationId = conversation.ConversationId;
                CacheService.caches[lineId] = userParams;
                watermark = null;
            //}
        }

        public async Task HandleBeaconEvent()
        {
        }

        public async Task HandleFollowEvent()
        {
        }

        public async Task HandleJoinEvent()
        {
        }

        public async Task HandleLeaveEvent()
        {
        }

        public async Task HandlePostbackEvent()
        {
            dl.Activity sendMessage = new dl.Activity()
            {
                Type = "message",
                Text = lineEvent.Postback.Data,
                From = new dl.ChannelAccount(lineEvent.Source.UserId, lineEvent.Source.UserId)
            };

            // Send the message, then fetch and reply messages,
            await dlClient.Conversations.PostActivityAsync(conversationId, sendMessage);
            await GetAndReplyMessages();
        }

        public async Task HandleUnfollowEvent()
        {
        }

        public async Task<Profile> GetProfile(string mid)
        {
            return await lineClient.GetProfile(mid);
        }

        public async Task HandleTextMessage()
        {
            var status = getStatusRegister(lineEvent.Source.UserId);
            var textMessage = JsonConvert.DeserializeObject<TextMessage>(lineEvent.Message.ToString());
            var chkcommand = true;
            List<Message> mes = new List<Message>();
            var listCommand = new string[] { "r#", "r-", "re", "R#", "R-" };
            if (listCommand.Contains(textMessage.Text.Substring(0, 2)))
            {
                var res = new ResponseModel();
                var text = textMessage.Text.Substring(0, 2).ToLower().ToString();
                switch (text)
                {
                    case "r-":
                        mes.Add(new TextMessage("กรุณารอสักครู่กำลังทำรายการ"));
                        await Reply(mes);
                        var str = textMessage.Text.Replace("r-", "");
                        mes.Clear();
                        res = ActivateLine(str, lineEvent.Source.UserId);
                        mes.Add(new TextMessage(res.messege));
                        await Reply(mes);
                        mes.Clear();
                        if (res.status) {
                            var message = new StickerMessage("2", "41");
                            await Reply(new List<Message>() { message });
                         
                        }
                        break;
                    case "r#":
                        if (status)
                        {

                            res = getUserList(lineEvent.Source.UserId);
                            if (res.status)
                            {
                                foreach (var r in (List<C_SM_INAC>)res.data)
                                {
                                    var button = new ButtonsTemplate(title: "แจ้งปัญหาการใช้งาน", text: "รหัสลูกค้า " + r.U_BpCode + "\n ที่อยู่ติดตั้ง " + r.U_InsTo);
                                    var strreport = textMessage.Text.Replace("r#", "");
                                    var card = new CardAction() { Type = "imBack", Title = "แจ้งปัญหา", Value = "report-" + r.U_BpCode + "#" + strreport };
                                    button.Actions.Add(GetAction(card));
                                    var messagesTemplate = new TemplateMessage("Buttons template", button);
                                    await Reply(new List<Message>() { messagesTemplate });
                                }
                            }
                        }
                        else {
                            chkcommand = false;
                        }
                        break;
                    default:
                        if (textMessage.Text.StartsWith("report-"))
                        {
                            if (status)
                            {
                                mes.Add(new TextMessage("กรุณารอสักครู่กำลังทำรายการนะจ้ะ"));
                                await Reply(mes);
                                mes.Clear();
                                var strres = textMessage.Text.Replace("report-", "");
                                var codereport = strres.Split('#');
                                var usercode = codereport[0].ToString();
                                var details = codereport[1].ToString();
                                var resreport = saveReportProblem(usercode, details);
                                mes.Add(new TextMessage(resreport.messege));
                                await Reply(mes);
                                mes.Clear();
                            }
                        }
                        else {
                            chkcommand = false;
                        }
                        break;
                }
            }
            else
                chkcommand = false;

            switch (textMessage.Text)
            {
                case "ลงทะเบียน":
                    mes.Add(new TextMessage("กรุณาพิมพ์ r-ตามด้วยรหัสลูกค้า"));
                    mes.Add(new TextMessage("ตัวอย่าง: r-50000001"));
                    break;
                case "แจ้งปัญหา":
                    if (status) {
                        mes.Add(new TextMessage("กรุณาพิมพ์ r# ตามด้วยปัญหา"));
                        mes.Add(new TextMessage("ตัวอย่าง: r#อินเตอร์เน็ตช้าไม่สามารถใช้งานได้บางช่วง"));
                    }  
                    else
                        mes.Add(new TextMessage("กรุณาลงทะเบียนก่อนใช้งาน"));
                    break;
                case "ตรวจสอบค่าบริการ":
                    if (status)
                    {
                        mes.Add(new TextMessage("กรุณารอสักครู่กำลังทำรายการ"));
                        await Reply(mes);
                        mes.Clear();
                        var res = getOverdue(lineEvent.Source.UserId);
                        if (res.status)
                        {
               
                            var listdata = (List<Invoice>)res.data;
                            if (listdata.Count() > 0)
                            {
                                mes.Add(new TextMessage("ค้างชำระค่าบริการทั้งหมด "+listdata.Count +" รายการ"));
                                int count = 1;
                                foreach (var r in listdata) {
                                    mes.Add(new TextMessage(count.ToString()+". ใบแจ้งค่าบริการหมายเลข "+r.InvoiceNo+"\n"+r.Comment+ "\n" +"จำนวนเงิน "+ Math.Round(r.Amount ?? 0,2) +" บาท"));
                                    count++;
                                }
                                if (count > 2) {
                                    var totalamount = res.amount ?? 0;
                                    mes.Add(new TextMessage("รวมทั้งสิ้น " + Math.Round(totalamount,2)+ " บาท"));
                                }
                            }
                            else {
                                mes.Add(new TextMessage(res.messege));
                            }
                        }
                        else
                        {
                            mes.Add(new TextMessage(res.messege));
                        }
                    }
                    else {
                        mes.Add(new TextMessage("กรุณาลงทะเบียนก่อนใช้งาน"));
                    }
                    break;
                default:
                    if(!chkcommand)
                        mes.Add(new TextMessage("ไม่พบคำสั่ง กรุณาตรวจสอบข้อมูล"));
                    break;
            }


            //await Reply(new List<Message>() { message });

            // Send the message, then fetch and reply messages,
            //try
            //{
            //    await dlClient.Conversations.PostActivityAsync(conversationId, sendMessage);
            //}
            //catch (Exception ex)
            //{

            //}
            if (mes.Count > 0) {
                await Reply(mes);
            }
           
        }

        public async Task HandleMediaMessage()
        {
            Message message = JsonConvert.DeserializeObject<Message>(lineEvent.Message.ToString());
            // Get media from Line server.
            var media = await lineClient.GetContent(message.Id);
            await dlClient.Conversations.UploadAsync(conversationId, media.Content, lineEvent.Source.UserId, media.ContentType);
            await GetAndReplyMessages();
        }

        public async Task HandleStickerMessage()
        {
            //https://devdocs.line.me/files/sticker_list.pdf
            var stickerMessage = JsonConvert.DeserializeObject<StickerMessage>(lineEvent.Message.ToString());
            var message = new StickerMessage("1", "1");
            await Reply(new List<Message>() { message });
        }

        public async Task HandleLocationMessage()
        {
            var locationMessage = JsonConvert.DeserializeObject<LocationMessage>(lineEvent.Message.ToString());

            dl.Activity sendMessage = new dl.Activity()
            {
                Type = "message",
                Text = locationMessage.Title,
                From = new dl.ChannelAccount(lineEvent.Source.UserId, lineEvent.Source.UserId),
                Entities = new List<Entity>()
                {
                    new Entity()
                    {
                        Type = "Place",
                        Properties = JObject.FromObject(new Place(address:locationMessage.Address,
                            geo:new dl.GeoCoordinates(
                                latitude: locationMessage.Latitude,
                                longitude: locationMessage.Longitude,
                                name: locationMessage.Title),
                            name:locationMessage.Title))
                    }
                }
            };

            // Send the message, then fetch and reply messages,
            await dlClient.Conversations.PostActivityAsync(conversationId, sendMessage);
            await GetAndReplyMessages();
        }

        private async Task Reply(List<Message> replyMessages)
        {
            int i = 0;
            try
            {
                await lineClient.ReplyToActivityAsync(lineEvent.CreateReply(
                       messages: replyMessages.Take(5).ToList()));

                if (replyMessages.Count > 5)
                {
                    i = 1;
                    while (replyMessages.Count > i * 5)
                    {
                        await lineClient.PushAsync(lineEvent.CreatePush(
                            messages: replyMessages.Skip(i * 5).Take(5).ToList()));
                        i++;
                    }
                }
            }
            catch
            {
                try
                {
                    while (replyMessages.Count > i * 5)
                    {
                        await lineClient.PushAsync(lineEvent.CreatePush(
                            messages: replyMessages.Skip(i * 5).Take(5).ToList()));
                        i++;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    await lineClient.PushAsync(lineEvent.CreatePush(ex.Message, message:null));
#endif
                }
            }
        }

        /// <summary>
        /// Get all messages from DirectLine and reply back to Line
        /// </summary>
        private async Task GetAndReplyMessages()
        {
            dl.ActivitySet result = string.IsNullOrEmpty(watermark) ?
                await dlClient.Conversations.GetActivitiesAsync(conversationId) :
                await dlClient.Conversations.GetActivitiesAsync(conversationId, watermark);

            userParams["Watermark"] = (Int64.Parse(result.Watermark)).ToString();

            foreach (var activity in result.Activities)
            {
                if (activity.From.Id == lineEvent.Source.UserId)
                    continue;

                List<Message> messages = new List<Message>();

                if (activity.Attachments != null && activity.Attachments.Count != 0 && (activity.AttachmentLayout == null || activity.AttachmentLayout == "list"))
                {
                    foreach (var attachment in activity.Attachments)
                    {
                        if (attachment.ContentType.Contains("card.animation"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#animationcard
                            // Use TextMessage for title and use Image message for image. Not really an animation though.
                            AnimationCard card = JsonConvert.DeserializeObject<AnimationCard>(attachment.Content.ToString());
                            messages.Add(new TextMessage($"{card.Title}\r\n{card.Subtitle}\r\n{card.Text}"));
                            foreach (var media in card.Media)
                            {
                                var originalContentUrl = media.Url?.Replace("http://", "https://");
                                var previewImageUrl = card.Image?.Url?.Replace("http://", "https://");
                                messages.Add(new ImageMessage(originalContentUrl, previewImageUrl));
                            }
                        }
                        else if (attachment.ContentType.Contains("card.audio"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#audiocard
                            // Use TextMessage for title and use Audio message for image.
                            AudioCard card = JsonConvert.DeserializeObject<AudioCard>(attachment.Content.ToString());
                            messages.Add(new TextMessage($"{card.Title}\r\n{card.Subtitle}\r\n{card.Text}"));

                            foreach (var media in card.Media)
                            {
                                var originalContentUrl = media.Url?.Replace("http://", "https://");
                                var durationInMilliseconds = 1;

                                messages.Add(new AudioMessage(originalContentUrl, durationInMilliseconds));
                            }
                        }
                        else if (attachment.ContentType.Contains("card.hero") || attachment.ContentType.Contains("card.thumbnail"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#herocard
                            // https://docs.botframework.com/en-us/core-concepts/reference/#thumbnailcard
                            HeroCard hcard = null;

                            if (attachment.ContentType.Contains("card.hero"))
                                hcard = JsonConvert.DeserializeObject<HeroCard>(attachment.Content.ToString());
                            else if (attachment.ContentType.Contains("card.thumbnail"))
                            {
                                ThumbnailCard tCard = JsonConvert.DeserializeObject<ThumbnailCard>(attachment.Content.ToString());
                                hcard = new HeroCard(tCard.Title, tCard.Subtitle, tCard.Text, tCard.Images, tCard.Buttons, null);
                            }

                            ButtonsTemplate buttonsTemplate = new ButtonsTemplate(
                                hcard.Images?.First().Url.Replace("http://", "https://"),
                                hcard.Subtitle == null ? null : hcard.Title,
                                string.IsNullOrEmpty(hcard.Subtitle) ? hcard.Text : hcard.Subtitle);

                            if (hcard.Buttons != null)
                            {
                                foreach (var button in hcard.Buttons)
                                {
                                    buttonsTemplate.Actions.Add(GetAction(button));
                                }
                            }
                            else
                            {
                                // Action is mandatory, so create from title/subtitle.
                                var actionLabel = hcard.Title?.Length < hcard.Subtitle?.Length ? hcard.Title : hcard.Subtitle;
                                buttonsTemplate.Actions.Add(new PostbackTemplateAction(actionLabel, actionLabel, actionLabel));
                            }

                            messages.Add(new TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("receipt"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#receiptcard
                            // Use TextMessage and Buttons. As LINE doesn't support thumbnail type yet.

                            ReceiptCard card = JsonConvert.DeserializeObject<ReceiptCard>(attachment.Content.ToString());
                            var text = card.Title + "\r\n\r\n";
                            foreach (var fact in card.Facts)
                            {
                                text += $"{fact.Key}:{fact.Value}\r\n";
                            }
                            text += "\r\n";
                            foreach (var item in card.Items)
                            {
                                text += $"{item.Title}\r\nprice:{item.Price},quantity:{item.Quantity}";
                            }

                            messages.Add(new TextMessage(text));

                            ButtonsTemplate buttonsTemplate = new ButtonsTemplate(title: $"total:{card.Total}", text: $"tax:{card.Tax}");
                            foreach (var button in card.Buttons)
                            {
                                buttonsTemplate.Actions.Add(GetAction(button));
                            }

                            messages.Add(new TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("card.signin"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#signincard
                            // Line doesn't support auth button yet, so simply represent link.
                            SigninCard card = JsonConvert.DeserializeObject<SigninCard>(attachment.Content.ToString());

                            ButtonsTemplate buttonsTemplate = new ButtonsTemplate(text: card.Text);
                            foreach (var button in card.Buttons)
                            {
                                buttonsTemplate.Actions.Add(GetAction(button));
                            }
                            messages.Add(new TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("card.video"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#videocard
                            // Use Video message for video and buttons for action.

                            VideoCard card = JsonConvert.DeserializeObject<VideoCard>(attachment.Content.ToString());

                            foreach (var media in card.Media)
                            {
                                var originalContentUrl = media?.Url?.Replace("http://", "https://");
                                var previewImageUrl = card.Image?.Url?.Replace("http://", "https://");

                                messages.Add(new VideoMessage(originalContentUrl, previewImageUrl));
                            }

                            ButtonsTemplate buttonsTemplate = new ButtonsTemplate(title: card.Title, text: $"{card.Subtitle}\r\n{card.Text}");
                            foreach (var button in card.Buttons)
                            {
                                buttonsTemplate.Actions.Add(GetAction(button));
                            }
                            messages.Add(new TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("image"))
                        {
                            var originalContentUrl = attachment.ContentUrl?.Replace("http://", "https://");
                            var previewImageUrl = string.IsNullOrEmpty(attachment.ThumbnailUrl) ? attachment.ContentUrl?.Replace("http://", "https://") : attachment.ThumbnailUrl?.Replace("http://", "https://");

                            messages.Add(new ImageMessage(originalContentUrl, previewImageUrl));
                        }
                        else if (attachment.ContentType.Contains("audio"))
                        {
                            var originalContentUrl = attachment.ContentUrl?.Replace("http://", "https://");
                            var durationInMilliseconds = 0;

                            messages.Add(new AudioMessage(originalContentUrl, durationInMilliseconds));
                        }
                        else if (attachment.ContentType.Contains("video"))
                        {
                            var originalContentUrl = attachment.ContentUrl?.Replace("http://", "https://");
                            var previewImageUrl = attachment.ThumbnailUrl?.Replace("http://", "https://");

                            messages.Add(new VideoMessage(originalContentUrl, previewImageUrl));
                        }
                    }
                }
                else if (activity.Attachments != null && activity.Attachments.Count != 0 && activity.AttachmentLayout != null)
                {
                    CarouselTemplate carouselTemplate = new CarouselTemplate();

                    foreach (var attachment in activity.Attachments)
                    {
                        HeroCard hcard = null;

                        if (attachment.ContentType == "application/vnd.microsoft.card.hero")
                            hcard = JsonConvert.DeserializeObject<HeroCard>(attachment.Content.ToString());
                        else if (attachment.ContentType == "application/vnd.microsoft.card.thumbnail")
                        {
                            ThumbnailCard tCard = JsonConvert.DeserializeObject<ThumbnailCard>(attachment.Content.ToString());
                            hcard = new HeroCard(tCard.Title, tCard.Subtitle, tCard.Text, tCard.Images, tCard.Buttons, null);
                        }
                        else
                            continue;

                        TemplateColumn tColumn = new TemplateColumn(
                            hcard.Images.FirstOrDefault()?.Url?.Replace("http://", "https://"),
                            hcard.Subtitle == null ? null : hcard.Title,
                            string.IsNullOrEmpty(hcard.Subtitle) ? hcard.Title : hcard.Subtitle);

                        if (hcard.Buttons != null)
                        {
                            foreach (var button in hcard.Buttons)
                            {
                                tColumn.Actions.Add(GetAction(button));
                            }
                        }
                        else
                        {
                            // Action is mandatory, so create from title/subtitle.
                            var actionLabel = hcard.Title?.Length < hcard.Subtitle?.Length ? hcard.Title : hcard.Subtitle;
                            tColumn.Actions.Add(new PostbackTemplateAction(actionLabel, actionLabel, actionLabel));
                        }

                        carouselTemplate.Columns.Add(tColumn);
                    }

                    messages.Add(new TemplateMessage("Carousel template", carouselTemplate));
                }
                else if (activity.Entities != null && activity.Entities.Count != 0)
                {
                    foreach (var entity in activity.Entities)
                    {
                        switch (entity.Type)
                        {
                            case "Place":
                                Place place = entity.Properties.ToObject<Place>();
                                GeoCoordinates geo = JsonConvert.DeserializeObject<GeoCoordinates>(place.Geo.ToString());
                                messages.Add(new LocationMessage(place.Name, place.Address.ToString(), geo.Latitude, geo.Longitude));
                                break;
                            case "GeoCoordinates":
                                GeoCoordinates geoCoordinates = entity.Properties.ToObject<GeoCoordinates>();
                                messages.Add(new LocationMessage(activity.Text, geoCoordinates.Name, geoCoordinates.Latitude, geoCoordinates.Longitude));
                                break;
                        }
                    }
                }
                else if (activity.ChannelData != null)
                {
                }
                else if (!string.IsNullOrEmpty(activity.Text))
                {
                    if (activity.Text.Contains("\n\n*"))
                    {
                        var lines = activity.Text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                        ButtonsTemplate buttonsTemplate = new ButtonsTemplate(text: lines[0]);

                        foreach (var line in lines.Skip(1))
                        {
                            buttonsTemplate.Actions.Add(new PostbackTemplateAction(line, line.Replace("* ", ""), line.Replace("* ", "")));
                        }

                        messages.Add(new TemplateMessage("Buttons template", buttonsTemplate));
                    }
                    else
                        messages.Add(new TextMessage(activity.Text));
                }

                await Reply(messages);
            }
        }

        /// <summary>
        /// Create TemplateAction from CardAction.
        /// </summary>
        /// <param name="button">CardAction</param>
        /// <returns>TemplateAction</returns>
        private TemplateAction GetAction(CardAction button)
        {
            switch (button.Type)
            {
                case "openUrl":
                case "playAudio":
                case "playVideo":
                case "showImage":
                case "signin":
                case "downloadFile":
                    return new UriTemplateAction(button.Title, button.Value.ToString());
                case "imBack":
                    return new MessageTemplateAction(button.Title, button.Value.ToString());
                case "postBack":
                    return new PostbackTemplateAction(button.Title, button.Value.ToString(), button.Value.ToString());
                default:
                    return null;
            }
        }

        private static ResponseModel ActivateLine(string text,string token) {
            var responseReturn = new ResponseModel();
            try
            {
                using (var dbFttx = new TFTTxEntities())
                using (var db = new InternetAccountEntities())
                {
                    var checkcustomer = dbFttx.C_SM_INAC.Where(r => r.U_BpCode == text).FirstOrDefault();
                    if (checkcustomer == null) {
                        responseReturn = new ResponseModel() { status = false , messege = "ไม่พบรหัสลูกค้านี้ภายในระบบ กรุณาตรวจสอบข้อมูลอีกครั้ง"};
                        return responseReturn;
                    }
                    var checkatv = db.LineActivate.Where(r => r.Cardcode == text).FirstOrDefault();
                    if (checkatv == null)
                    {
                        db.LineActivate.Add(new LineActivate()
                        {
                            Cardcode = text,
                            LineToken = token
                        });
                        db.SaveChanges();
                        var datauser = dbFttx.C_SM_INAC.Where(r => r.U_BpCode == text).FirstOrDefault();
                        responseReturn = new ResponseModel { status = true, messege = "ลงทะเบียนการใช้งานในชื่อ " + datauser.U_BpName + "เรียบร้อยแล้ว" };
                    }
                    else
                    {
                        responseReturn = new ResponseModel { status = false, messege = "ผู้ใช้งานนี้มีการลงทะเบียนแล้วไม่สามารถลงทะเบียนซ้ำได้" };
                    }
                }
            }
            catch (Exception ex) {
                responseReturn = new ResponseModel { status = false, messege = "พบข้อผิดพลาดไม่สามารถลงทะเบียนได้ กรุณาติดต่อ 1147" };
            }
           
            return responseReturn;
        }
        private static bool getStatusRegister(string token) {
            using (var db = new InternetAccountEntities()) {
                var data = db.LineActivate.Where(r => r.LineToken == token).FirstOrDefault();
                if (data == null)
                {
                    return false;
                }
                else {
                    return true;
                }
            }
        }
        private static ResponseModel getOverdue(string token) {
            var responseReturn = new ResponseModel();
            try
            {
                var customercode = string.Empty;
                using (var db = new InternetAccountEntities()) {
                    customercode = db.LineActivate.Where(r => r.LineToken == token).Select(r => r.Cardcode).FirstOrDefault();
                }
                if (string.IsNullOrEmpty(customercode))
                {
                    responseReturn = new ResponseModel { status = false, messege = "ไม่พบข้อมูลการลงทะเบียนลูกค้าจาก Line ID นี้" };
                    return responseReturn;
                }
                else {
                    using (var dbFttx = new TFTTxEntities())
                    {
                        var data = (from t1 in dbFttx.OINV
                                    join t2 in dbFttx.C_SM_INAC on t1.CardCode equals t2.U_BpCode
                                    join t3 in dbFttx.OCRD on t1.CardCode equals t3.CardCode
                                    join t4 in dbFttx.OITM on t2.U_PckCode equals t4.ItemCode
                                    join t5 in dbFttx.C_PLANBDATA on t2.U_PlanBCode equals t5.U_ProjectCode
                                    where (t1.DocStatus == "O")
                                    where (DbFunctions.DiffDays(t1.DocDate, DateTime.Now) > 7)
                                    where (t1.CardCode == customercode)
                                    select new Invoice
                                    {
                                        InvoiceNo = t1.DocNum.ToString(),
                                        Comment = t1.Comments,
                                        Amount = t1.DocTotal
                                    }).ToList();
                        if (data.Count > 0)
                        {
                            responseReturn = new ResponseModel { status = true, data = data, total = data.Count, amount = data.Sum(r => r.Amount) };
                        }
                        else
                        {
                            responseReturn = new ResponseModel { status = false, messege = "ไม่พบข้อมูลค้างชำระค่าบริการ" };
                        }
                    }
                }
               
            }
            catch (Exception ex) {
                responseReturn = new ResponseModel { status = false, messege = "ไม่สามารถทำรายการได้ กรุณาติดต่อ call center 1147" };
            }
            
            return responseReturn;
        }
        private static ResponseModel getUserList(string token) {
            var responseReturn = new ResponseModel();
            using (var dbFttx = new TFTTxEntities())
            using (var db = new InternetAccountEntities()) {
                var listuser = db.LineActivate.Where(x => x.LineToken == token).Select(x => x.Cardcode).ToList();
                var data = dbFttx.C_SM_INAC.Where(r => listuser.Contains(r.U_BpCode)).ToList();
                if (data.Count > 0)
                {
                    responseReturn = new ResponseModel { status = true, messege = "",data = data };
                }
                else {
                    responseReturn = new ResponseModel { status = false, messege = "ไม่พบข้อมูลผู้ใช้งาน" };
                }
            }
            return responseReturn;
        }
        private static ResponseModel saveReportProblem(string cardcode, string details) {
            var responseReturn = new ResponseModel();
            try
            {
                using (var dbFttx = new TFTTxEntities())
                using (var db = new InternetAccountEntities())
                {
                    var data = dbFttx.C_SM_INAC.Where(r => r.U_BpCode == cardcode).FirstOrDefault();
                    if (data == null)
                    {
                        responseReturn = new ResponseModel { status = false, messege = "ไม่พบข้อมูลผู้ใช้งานกรุณาตรวจสอบข้อมูลหรือติดต่อ call center 1147" };
                    }
                    else
                    {
                        db.ReportProblemLine.Add(new ReportProblemLine
                        {
                            Cardcode = cardcode,
                            ProblemDetail = details,
                            CreateDate = DateTime.Now
                        });
                        db.SaveChanges();
                        responseReturn = new ResponseModel { status = true, messege = "บันทึกข้อมูลเรียบร้อยแล้ว" };
                    }
                }
            }
            catch (Exception ex) {
                responseReturn = new ResponseModel { status = false, messege = "พบปัญหาในการบันทึกข้อมูลกรุณาติดต่อ call center 1147" };
            }
            
            return responseReturn;
        }
        
    }
}
