// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Exit Games GmbH"/>
// <summary>Demo code for Photon Chat in Unity.</summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------


using UnityEngine;
using UnityEngine.UI;

namespace Photon.Chat.Demo
{
    [RequireComponent(typeof(ChatGui))]
    public class NamePickGui : MonoBehaviour
    {
        private const string UserNamePlayerPref = "NamePickUserName";

        public ChatGui chatNewComponent;

        public InputField idInput;

        public void Start()
        {
            #if UNITY_6000_0_OR_NEWER
            chatNewComponent = FindFirstObjectByType<ChatGui>();
            #else
            this.chatNewComponent = FindObjectOfType<ChatGui>();
            #endif

            string prefsName = PlayerPrefs.GetString(UserNamePlayerPref);
            if (!string.IsNullOrEmpty(prefsName))
            {
                idInput.text = prefsName;
            }
        }


        // new UI will fire "EndEdit" event also when loosing focus. So check "enter" key and only then StartChat.
        public void EndEditOnEnter()
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            {
                StartChat();
            }
        }

        public void StartChat()
        {
            #if UNITY_6000_0_OR_NEWER
            ChatGui chatNewComponent = FindFirstObjectByType<ChatGui>();
            #else
            ChatGui chatNewComponent = FindObjectOfType<ChatGui>();
            #endif

            chatNewComponent.UserName = idInput.text.Trim();
            chatNewComponent.Connect();
            enabled = false;

            PlayerPrefs.SetString(UserNamePlayerPref, chatNewComponent.UserName);
        }
    }
}