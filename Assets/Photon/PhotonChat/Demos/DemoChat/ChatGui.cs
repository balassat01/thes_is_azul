// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Exit Games GmbH"/>
// <summary>Demo code for Photon Chat in Unity.</summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif


namespace Photon.Chat.Demo
{
    /// <summary>
    /// This simple Chat UI demonstrate basics usages of the Chat Api
    /// </summary>
    /// <remarks>
    /// The ChatClient basically lets you create any number of channels.
    ///
    /// some friends are already set in the Chat demo "DemoChat-Scene", 'Joe', 'Jane' and 'Bob', simply log with them so that you can see the status changes in the Interface
    ///
    /// Workflow:
    /// Create ChatClient, Connect to a server with your AppID, Authenticate the user (apply a unique name,)
    /// and subscribe to some channels.
    /// Subscribe a channel before you publish to that channel!
    ///
    ///
    /// Note:
    /// Don't forget to call ChatClient.Service() on Update to keep the Chatclient operational.
    /// </remarks>
    public class ChatGui : MonoBehaviour, IChatClientListener
    {

        public string[] ChannelsToJoinOnConnect; // set in inspector. Demo channels to join automatically.

        public string[] FriendsList;

        public int HistoryLengthToFetch; // set in inspector. Up to a certain degree, previously sent messages can be fetched for context

        public string UserName { get; set; }

        private string selectedChannelName; // mainly used for GUI/input

        public ChatClient chatClient;

        #if !PHOTON_UNITY_NETWORKING
        public ChatAppSettings ChatAppSettings
        {
            get { return this.chatAppSettings; }
        }

        [SerializeField]
        #endif
        protected internal ChatAppSettings chatAppSettings;


        public GameObject missingAppIdErrorPanel;
        public GameObject ConnectingLabel;

        public RectTransform ChatPanel;     // set in inspector (to enable/disable panel)
        public GameObject UserIdFormPanel;
        public InputField InputFieldChat;   // set in inspector
        public Text CurrentChannelText;     // set in inspector
        public Toggle ChannelToggleToInstantiate; // set in inspector


        public GameObject FriendListUiItemtoInstantiate;

        private readonly Dictionary<string, Toggle> channelToggles = new Dictionary<string, Toggle>();

        private readonly Dictionary<string,FriendItem> friendListItemLUT =  new Dictionary<string, FriendItem>();

        public bool ShowState = true;
        public GameObject Title;
        public Text StateText; // set in inspector
        public Text UserIdText; // set in inspector

        // private static string WelcomeText = "Welcome to chat. Type \\help to list commands.";
        private static string HelpText = "\n    -- HELP --\n" +
            "To subscribe to channel(s) (channelnames are case sensitive) :  \n" +
                "\t<color=#E07B00>\\subscribe</color> <color=green><list of channelnames></color>\n" +
                "\tor\n" +
                "\t<color=#E07B00>\\s</color> <color=green><list of channelnames></color>\n" +
                "\n" +
                "To leave channel(s):\n" +
                "\t<color=#E07B00>\\unsubscribe</color> <color=green><list of channelnames></color>\n" +
                "\tor\n" +
                "\t<color=#E07B00>\\u</color> <color=green><list of channelnames></color>\n" +
                "\n" +
                "To switch the active channel\n" +
                "\t<color=#E07B00>\\join</color> <color=green><channelname></color>\n" +
                "\tor\n" +
                "\t<color=#E07B00>\\j</color> <color=green><channelname></color>\n" +
                "\n" +
                "To send a private message: (username are case sensitive)\n" +
                "\t\\<color=#E07B00>msg</color> <color=green><username></color> <color=green><message></color>\n" +
                "\n" +
                "To change status:\n" +
                "\t\\<color=#E07B00>state</color> <color=green><stateIndex></color> <color=green><message></color>\n" +
                "<color=green>0</color> = Offline " +
                "<color=green>1</color> = Invisible " +
                "<color=green>2</color> = Online " +
                "<color=green>3</color> = Away \n" +
                "<color=green>4</color> = Do not disturb " +
                "<color=green>5</color> = Looking For Group " +
                "<color=green>6</color> = Playing" +
                "\n\n" +
                "To clear the current chat tab (private chats get closed):\n" +
                "\t<color=#E07B00>\\clear</color>";


        public void Start()
        {
            DontDestroyOnLoad(gameObject);

            UserIdText.text = "";
            StateText.text  = "";
            StateText.gameObject.SetActive(true);
            UserIdText.gameObject.SetActive(true);
            Title.SetActive(true);
            ChatPanel.gameObject.SetActive(false);
            ConnectingLabel.SetActive(false);

            if (string.IsNullOrEmpty(UserName))
            {
                UserName = "user" + Environment.TickCount%99; //made-up username
            }

            #if PHOTON_UNITY_NETWORKING
            chatAppSettings = PhotonNetwork.PhotonServerSettings.AppSettings.GetChatSettings();
            #endif

            bool appIdPresent = !string.IsNullOrEmpty(chatAppSettings.AppIdChat);

            missingAppIdErrorPanel.SetActive(!appIdPresent);
            UserIdFormPanel.gameObject.SetActive(appIdPresent);

            if (!appIdPresent)
            {
                Debug.LogError("You need to set the chat app ID in the PhotonServerSettings file in order to continue.");
            }
        }

        public void Connect()
        {
            UserIdFormPanel.gameObject.SetActive(false);

            chatClient = new ChatClient(this);
            #if !UNITY_WEBGL
            chatClient.UseBackgroundWorkerForSending = true;
            #endif
            chatClient.AuthValues = new AuthenticationValues(UserName);
            chatClient.ConnectUsingSettings(chatAppSettings);

            ChannelToggleToInstantiate.gameObject.SetActive(false);
            Debug.Log("Connecting as: " + UserName);

            ConnectingLabel.SetActive(true);
        }

        /// <summary>To avoid that the Editor becomes unresponsive, disconnect all Photon connections in OnDestroy.</summary>
        public void OnDestroy()
        {
            if (chatClient != null)
            {
                chatClient.Disconnect();
            }
        }

        /// <summary>To avoid that the Editor becomes unresponsive, disconnect all Photon connections in OnApplicationQuit.</summary>
        public void OnApplicationQuit()
        {
            if (chatClient != null)
            {
                chatClient.Disconnect();
            }
        }

        public void Update()
        {
            if (chatClient != null)
            {
                chatClient.Service(); // make sure to call this regularly! it limits effort internally, so calling often is ok!
            }

            // check if we are missing context, which means we got kicked out to get back to the Photon Demo hub.
            if ( StateText == null)
            {
                Destroy(gameObject);
                return;
            }

            StateText.gameObject.SetActive(ShowState); // this could be handled more elegantly, but for the demo it's ok.
        }


        public void OnEnterSend()
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            {
                SendChatMessage(InputFieldChat.text);
                InputFieldChat.text = "";
            }
        }

        public void OnClickSend()
        {
            if (InputFieldChat != null)
            {
                SendChatMessage(InputFieldChat.text);
                InputFieldChat.text = "";
            }
        }


        public int TestLength = 2048;
        private byte[] testBytes = new byte[2048];

        private void SendChatMessage(string inputLine)
        {
            if (string.IsNullOrEmpty(inputLine))
            {
                return;
            }
            if ("test".Equals(inputLine))
            {
                if (TestLength != testBytes.Length)
                {
                    testBytes = new byte[TestLength];
                }

                chatClient.SendPrivateMessage(chatClient.AuthValues.UserId, testBytes, true);
            }


            bool doingPrivateChat = chatClient.PrivateChannels.ContainsKey(selectedChannelName);
            string privateChatTarget = string.Empty;
            if (doingPrivateChat)
            {
                // the channel name for a private conversation is (on the client!!) always composed of both user's IDs: "this:remote"
                // so the remote ID is simple to figure out

                string[] splitNames = selectedChannelName.Split(new[] { ':' });
                privateChatTarget = splitNames[1];
            }
            //UnityEngine.Debug.Log("selectedChannelName: " + selectedChannelName + " doingPrivateChat: " + doingPrivateChat + " privateChatTarget: " + privateChatTarget);


            if (inputLine[0].Equals('\\'))
            {
                string[] tokens = inputLine.Split(new[] {' '}, 2);
                if (tokens[0].Equals("\\help"))
                {
                    PostHelpToCurrentChannel();
                }
                if (tokens[0].Equals("\\state"))
                {
                    int newState = 0;


                    List<string> messages = new List<string>();
                    messages.Add ("i am state " + newState);
                    string[] subtokens = tokens[1].Split(' ', ',');

                    if (subtokens.Length > 0)
                    {
                        newState = int.Parse(subtokens[0]);
                    }

                    if (subtokens.Length > 1)
                    {
                        messages.Add(subtokens[1]);
                    }

                    chatClient.SetOnlineStatus(newState,messages.ToArray()); // this is how you set your own state and (any) message
                }
                else if ((tokens[0].Equals("\\subscribe") || tokens[0].Equals("\\s")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    chatClient.Subscribe(tokens[1].Split(' ', ','));
                }
                else if ((tokens[0].Equals("\\unsubscribe") || tokens[0].Equals("\\u")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    chatClient.Unsubscribe(tokens[1].Split(' ', ','));
                }
                else if (tokens[0].Equals("\\clear"))
                {
                    if (doingPrivateChat)
                    {
                        chatClient.PrivateChannels.Remove(selectedChannelName);
                    }
                    else
                    {
                        ChatChannel channel;
                        if (chatClient.TryGetChannel(selectedChannelName, doingPrivateChat, out channel))
                        {
                            channel.ClearMessages();
                        }
                    }
                }
                else if (tokens[0].Equals("\\msg") && !string.IsNullOrEmpty(tokens[1]))
                {
                    string[] subtokens = tokens[1].Split(new[] {' ', ','}, 2);
                    if (subtokens.Length < 2) return;

                    string targetUser = subtokens[0];
                    string message = subtokens[1];
                    chatClient.SendPrivateMessage(targetUser, message);
                }
                else if ((tokens[0].Equals("\\join") || tokens[0].Equals("\\j")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    string[] subtokens = tokens[1].Split(new[] { ' ', ',' }, 2);

                    // If we are already subscribed to the channel we directly switch to it, otherwise we subscribe to it first and then switch to it implicitly
                    if (channelToggles.ContainsKey(subtokens[0]))
                    {
                        ShowChannel(subtokens[0]);
                    }
                    else
                    {
                        chatClient.Subscribe(new[] { subtokens[0] });
                    }
                }
                #if CHAT_EXTENDED
                else if ((tokens[0].Equals("\\nickname") || tokens[0].Equals("\\nick") ||tokens[0].Equals("\\n")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    if (!doingPrivateChat)
                    {
                        this.chatClient.SetCustomUserProperties(this.selectedChannelName, this.chatClient.UserId, new Dictionary<string, object> {{"Nickname", tokens[1]}});
                    }

                }
                #endif
                else
                {
                    Debug.Log("The command '" + tokens[0] + "' is invalid.");
                }
            }
            else
            {
                if (doingPrivateChat)
                {
                    chatClient.SendPrivateMessage(privateChatTarget, inputLine);
                }
                else
                {
                    chatClient.PublishMessage(selectedChannelName, inputLine);
                }
            }
        }

        public void PostHelpToCurrentChannel()
        {
            CurrentChannelText.text += HelpText;
        }

        public void DebugReturn(DebugLevel level, string message)
        {
            if (level == DebugLevel.ERROR)
            {
                Debug.LogError(message);
            }
            else if (level == DebugLevel.WARNING)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        public void OnConnected()
        {
            if (ChannelsToJoinOnConnect != null && ChannelsToJoinOnConnect.Length > 0)
            {
                chatClient.Subscribe(ChannelsToJoinOnConnect, HistoryLengthToFetch);
            }

            ConnectingLabel.SetActive(false);

            UserIdText.text = "Connected as "+ UserName;

            ChatPanel.gameObject.SetActive(true);

            if (FriendsList!=null  && FriendsList.Length>0)
            {
                chatClient.AddFriends(FriendsList); // Add some users to the server-list to get their status updates

                // add to the UI as well
                foreach(string _friend in FriendsList)
                {
                    if (FriendListUiItemtoInstantiate != null && _friend!= UserName)
                    {
                        InstantiateFriendButton(_friend);
                    }

                }

            }

            if (FriendListUiItemtoInstantiate != null)
            {
                FriendListUiItemtoInstantiate.SetActive(false);
            }


            chatClient.SetOnlineStatus(ChatUserStatus.Online); // You can set your online state (without a mesage).
        }

        public void OnDisconnected()
        {
            Debug.Log("OnDisconnected()");
            ConnectingLabel.SetActive(false);
        }

        public void OnChatStateChange(ChatState state)
        {
            // use OnConnected() and OnDisconnected()
            // this method might become more useful in the future, when more complex states are being used.

            StateText.text = state.ToString();
        }

        public void OnSubscribed(string[] channels, bool[] results)
        {
            // in this demo, we simply send a message into each channel. This is NOT a must have!
            foreach (string channel in channels)
            {
                chatClient.PublishMessage(channel, "says 'hi'."); // you don't HAVE to send a msg on join but you could.

                if (ChannelToggleToInstantiate != null)
                {
                    InstantiateChannelButton(channel);

                }
            }

            Debug.Log("OnSubscribed: " + string.Join(", ", channels));

            /*
            // select first subscribed channel in alphabetical order
            if (this.chatClient.PublicChannels.Count > 0)
            {
                var l = new List<string>(this.chatClient.PublicChannels.Keys);
                l.Sort();
                string selected = l[0];
                if (this.channelToggles.ContainsKey(selected))
                {
                    ShowChannel(selected);
                    foreach (var c in this.channelToggles)
                    {
                        c.Value.isOn = false;
                    }
                    this.channelToggles[selected].isOn = true;
                    AddMessageToSelectedChannel(WelcomeText);
                }
            }
            */

            // Switch to the first newly created channel
            ShowChannel(channels[0]);
        }

        /// <inheritdoc />
        public void OnSubscribed(string channel, string[] users, Dictionary<object, object> properties)
        {
            Debug.LogFormat("OnSubscribed: {0}, users.Count: {1} Channel-props: {2}.", channel, users.Length, properties.ToStringFull());
        }

        private void InstantiateChannelButton(string channelName)
        {
            if (channelToggles.ContainsKey(channelName))
            {
                Debug.Log("Skipping creation for an existing channel toggle.");
                return;
            }

            Toggle cbtn = Instantiate(ChannelToggleToInstantiate);
            cbtn.gameObject.SetActive(true);
            cbtn.GetComponentInChildren<ChannelSelector>().SetChannel(channelName);
            cbtn.transform.SetParent(ChannelToggleToInstantiate.transform.parent, false);

            channelToggles.Add(channelName, cbtn);
        }

        private void InstantiateFriendButton(string friendId)
        {
            GameObject fbtn = Instantiate(FriendListUiItemtoInstantiate);
            fbtn.gameObject.SetActive(true);
            FriendItem  _friendItem =	fbtn.GetComponent<FriendItem>();

            _friendItem.FriendId = friendId;

            fbtn.transform.SetParent(FriendListUiItemtoInstantiate.transform.parent, false);

            friendListItemLUT[friendId] = _friendItem;
        }


        public void OnUnsubscribed(string[] channels)
        {
            foreach (string channelName in channels)
            {
                if (channelToggles.ContainsKey(channelName))
                {
                    Toggle t = channelToggles[channelName];
                    Destroy(t.gameObject);

                    channelToggles.Remove(channelName);

                    Debug.Log("Unsubscribed from channel '" + channelName + "'.");

                    // Showing another channel if the active channel is the one we unsubscribed from before
                    if (channelName == selectedChannelName && channelToggles.Count > 0)
                    {
                        IEnumerator<KeyValuePair<string, Toggle>> firstEntry = channelToggles.GetEnumerator();
                        firstEntry.MoveNext();

                        ShowChannel(firstEntry.Current.Key);

                        firstEntry.Current.Value.isOn = true;
                    }
                }
                else
                {
                    Debug.Log("Can't unsubscribe from channel '" + channelName + "' because you are currently not subscribed to it.");
                }
            }
        }

        public void OnGetMessages(string channelName, string[] senders, object[] messages)
        {
            if (channelName.Equals(selectedChannelName))
            {
                // update text
                ShowChannel(selectedChannelName);
            }
        }

        public void OnPrivateMessage(string sender, object message, string channelName)
        {
            // as the ChatClient is buffering the messages for you, this GUI doesn't need to do anything here
            // you also get messages that you sent yourself. in that case, the channelName is determinded by the target of your msg
            InstantiateChannelButton(channelName);

            byte[] msgBytes = message as byte[];
            if (msgBytes != null)
            {
                Debug.Log("Message with byte[].Length: "+ msgBytes.Length);
            }
            if (selectedChannelName.Equals(channelName))
            {
                ShowChannel(channelName);
            }
        }

        /// <summary>
        /// New status of another user (you get updates for users set in your friends list).
        /// </summary>
        /// <param name="user">Name of the user.</param>
        /// <param name="status">New status of that user.</param>
        /// <param name="gotMessage">True if the status contains a message you should cache locally. False: This status update does not include a
        /// message (keep any you have).</param>
        /// <param name="message">Message that user set.</param>
        public void OnStatusUpdate(string user, int status, bool gotMessage, object message)
        {

            Debug.LogWarning("status: " + string.Format("{0} is {1}. Msg:{2}", user, status, message));

            if (friendListItemLUT.ContainsKey(user))
            {
                FriendItem _friendItem = friendListItemLUT[user];
                if ( _friendItem!=null) _friendItem.OnFriendStatusUpdate(status,gotMessage,message);
            }
        }

        public void OnUserSubscribed(string channel, string user)
        {
            Debug.LogFormat("OnUserSubscribed: channel=\"{0}\" userId=\"{1}\"", channel, user);
        }

        public void OnUserUnsubscribed(string channel, string user)
        {
            Debug.LogFormat("OnUserUnsubscribed: channel=\"{0}\" userId=\"{1}\"", channel, user);
        }

        /// <inheritdoc />
        public void OnChannelPropertiesChanged(string channel, string userId, Dictionary<object, object> properties)
        {
            Debug.LogFormat("OnChannelPropertiesChanged: {0} by {1}. Props: {2}.", channel, userId, properties.ToStringFull());
        }

        public void OnUserPropertiesChanged(string channel, string targetUserId, string senderUserId, Dictionary<object, object> properties)
        {
            Debug.LogFormat("OnUserPropertiesChanged: (channel:{0} user:{1}) by {2}. Props: {3}.", channel, targetUserId, senderUserId, properties.ToStringFull());
        }

        /// <inheritdoc />
        public void OnErrorInfo(string channel, string error, object data)
        {
            Debug.LogFormat("OnErrorInfo for channel {0}. Error: {1} Data: {2}", channel, error, data);
        }

        public void AddMessageToSelectedChannel(string msg)
        {
            ChatChannel channel = null;
            bool found = chatClient.TryGetChannel(selectedChannelName, out channel);
            if (!found)
            {
                Debug.Log("AddMessageToSelectedChannel failed to find channel: " + selectedChannelName);
                return;
            }

            if (channel != null)
            {
                channel.Add("Bot", msg,0); //TODO: how to use msgID?
            }
        }



        public void ShowChannel(string channelName)
        {
            if (string.IsNullOrEmpty(channelName))
            {
                return;
            }

            ChatChannel channel = null;
            bool found = chatClient.TryGetChannel(channelName, out channel);
            if (!found)
            {
                Debug.Log("ShowChannel failed to find channel: " + channelName);
                return;
            }

            selectedChannelName = channelName;
            CurrentChannelText.text = channel.ToStringMessages();
            Debug.Log("ShowChannel: " + selectedChannelName);

            foreach (KeyValuePair<string, Toggle> pair in channelToggles)
            {
                pair.Value.isOn = pair.Key == channelName ? true : false;
            }
        }

        public void OpenDashboard()
        {
            Application.OpenURL("https://dashboard.photonengine.com");
        }




    }
}