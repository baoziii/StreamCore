using StreamCore.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Bilibili
{
    public class BilibiliMessageHandlers : GenericMessageHandler<BilibiliMessage>
    {
        #region Message Handler Dictionaries
        private static Dictionary<string, Action<BilibiliMessage>> _onMessageReceived_Callbacks = new Dictionary<string, Action<BilibiliMessage>>();
        private static Dictionary<string, Action> _onInitialize_Callbacks = new Dictionary<string, Action>();
        #endregion

        public static Action OnInitialize
        {
            set { lock (_onInitialize_Callbacks) { _onInitialize_Callbacks[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onInitialize_Callbacks.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Bilibili OnMessageReceived event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<BilibiliMessage> OnMessageReceived
        {
            set { lock (_onMessageReceived_Callbacks) { _onMessageReceived_Callbacks[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onMessageReceived_Callbacks.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        internal static void Initialize()
        {
            if (Initialized)
                return;

            // Initialize our message handlers
            _messageHandlers.Add("Bilibili#onInitialize", OnInitialize_Handler);
            _messageHandlers.Add("Bilibili#liveChatMessage", OnMessageReceived_Handler);

            Initialized = true;
        }

        internal static void OnInitialize_Handler(BilibiliMessage message, string assemblyHash) 
        {
            SafeInvoke(_onInitialize_Callbacks, assemblyHash);
        }

        internal static void OnMessageReceived_Handler(BilibiliMessage message, string assemblyHash)
        {
            SafeInvoke(_onMessageReceived_Callbacks, message, assemblyHash);
        }
    }
}
