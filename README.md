# Mod Info
StreamCore is an IPA based mod designed to allow developers to create interactive chat experiences with ease. At the time of writing StreamCore supports Twitch, YouTube and Bilibili.

# Config File (配置文件)
Config files can be found in the `UserData\StreamCore` folder. This folder will be referenced as the `config folder` for the remainder of this readme.

你可以在游戏的 `UserData\StreamCore` 文件夹中找到与以下说明相对应的配置文件。

# Bilibili Config (哔哩哔哩配置)

Bilibili configuration has lots of settings. You can control the plugin to display which type of message as you want. Most of the features are not require for user info. Bilibili config file described below.

哔哩哔哩的配置项可以让你自由的选择想要显示的弹幕种类。大部分的功能不需要任何用户信息。

### BilibiliLoginInfo.ini

| Option (配置项)       | Description (描述)                                           |
| --------------------- | ------------------------------------------------------------ |
| **BilibiliChannelId** | The channel id of the Bilibili live channel you want to join (both short id or long id are supported) |
| **BilibiliCookie**    | Your bilibili cookie for the account you want to send messages as in chat (Not Supported yet, leave it blank) |
| **danmuku**           | If you want to display danmuku (include Super Chat Message) message, set to 1, other wise 0 |
| **popularity**        | If you want to display channel popularity, set to 1, other wise 0 |
| **gift**              | If you want to display gift (include Super Chat Message) message, set to 1, other wise 0 |
| **guard**             | If you want to display guard info, set to 1, other wise 0    |
| **welcome**           | If you want to display welcome banner, set to 1, other wise 0 |
| **anchor**            | If you want to display anchor info, set to 1, other wise 0   |
| **global**            | If you want to display bilibili global message, set to 1, other wise 0 |
| **blacklist**         | If you want to display channel blacklist update info, set to 1, other wise 0 |
| **roomInfo**          | If you want to display channel update info, set to 1, other wise 0 |
| **junk**              | If you want to display junks or unsupported message, set to 1, other wise 0 |

| 配置项                | 描述                                                         |
| --------------------- | ------------------------------------------------------------ |
| **BilibiliChannelId** | 直播间房间号(长号和短号都支持)                               |
| **BilibiliCookie**    | 你的哔哩哔哩账号Cookie(一般在网页版登陆后可以找到，现在没有用到它的必要，留空就行) |
| **danmuku**           | 弹幕信息显示开关(包括醒目留言)。1为开，0为关                 |
| **popularity**        | 人气值显示开关(包括醒目留言)。1为开，0为关                   |
| **gift**              | 礼物信息显示开关(包括醒目留言)。1为开，0为关                 |
| **guard**             | 舰长信息显示开关(舰长进入，上舰等)。1为开，0为关             |
| **welcome**           | 欢迎信息显示开关(老爷入场，舰长入场)。1为开，0为关           |
| **anchor**            | 天选之子显示开关。1为开，0为关                               |
| **global**            | 全站广播显示开关。1为开，0为关                               |
| **blacklist**         | 黑名单变动信息显示开关。1为开，0为关                         |
| **roomInfo**          | 房间信息变动显示开关。1为开，0为关                           |
| **junk**              | 垃圾消息以及暂不支持信息的显示开关。1为开，0为关             |

# Twitch Config

Twitch configuration is very straightforward, click the link below to generate an OAuth token and copy it into the Twitch login file described below.

### TwitchLoginInfo.ini
| Option | Description |
| - | - |
| **TwitchChannelName** | The name of the Twitch channel whos chat you want to join (this is your Twitch username if you want to join your own channel) |
| **TwitchUsername** | Your twitch username for the account you want to send messages as in chat (only matters if you're using the request bot) |
| **TwitchOAuthToken** | The oauth token corresponding to the TwitchUsername entered above ([Click here to generate an oauth token](https://twitchapps.com/tmi/))  |

# YouTube Config

## Important Info About the YouTube API
YouTube configuration is not so straightforward. It also isn't good for longer streams since the YouTube API is absolutely terrible and uses your YouTube APIv3 data quota to *read the chat*. 

What this means in layman's terms is that in a given day, you'll only be able to use StreamCore for about ~2 ish hours before you run out of quota. Sorry about this, I'll be implementing a way to use multiple sets of auth info in the future to "fix" this.

## YouTube Setup Instructions
### Step 1
First, head over to the [Google Developer API Console](https://console.developers.google.com), and if you don't have one already create a project. It doesn't matter what you name this project, but I would name it something related to StreamCore.

### Step 2
After you've created a project, click on the Credentials tab on the left and click the "create credentials" button, then click "OAuth client ID"

![Credentials](https://i.imgur.com/bE8HPzc.png)

### Step 3
Select application type "other" and enter a name, then click "Create".
![Credentials](https://i.imgur.com/m9ueTKI.png)

### Step 4
From the Credentials tab, click on the new OAuth credential you just created. You should see a screen with the client id/client secret for the OAuth credential you just created, click the "Download JSON" button and save it to your computer.
![OAuth](https://i.imgur.com/5yJInIR.png)

### Step 5
Rename the JSON file you just downloaded to `YouTubeClientId.json` and copy it into the StreamCore config folder. 

### Step 6
Now that we've got our OAuth credential, we need to enable the YouTube Data APIv3. To do this, head over to the [Google API Library](https://console.developers.google.com/apis/library).

### Step 7
Search for the "YouTube Data API v3" and click on it, then click "Enable".
![Library](https://i.imgur.com/ZT8eezQ.png)

### Step 8
Start up the game and a browser window should popup with the Google OAuth consent screen asking you to approve access to your account. After granting approval, StreamCore will automatically start reading chat from your live broadcast.


# Basic StreamCore Implementation (for devs)
Implementing StreamCore into your plugin is very simple, and can be accomplished in just a few steps.

## StreamCore version 2.x (Current Version)
### Step 1
Implement `ITwitchIntegration`, `IYouTubeIntegration`, or `IGlobalChatIntegration` into the class which you want to receive the chat callbacks

**Note:** StreamCore will automatically instantiate instances of any classes that implement any `IGenericChatIntegration`-based interface in OnApplicationStart, so make sure you don't instantiate these classes anywhere in your own code!

### Step 2
Setup any chat message callbacks you wish to subscribe to. For Twitch, subscribe to the events in `TwitchMessageHandlers`, and likewise for YouTube subscribe to the events in `YouTubeMessageHandlers`. Any future integrations will follow this naming convention.

### Step 3
After you have setup the chat message callbacks and your class is ready, set the `IsPluginReady` property to `true` (this is part of the `IGenericChatIntegration` interface, so you'll see what I mean once you do step 1).

**Note:** As long as `IsPluginReady` is set to false, StreamCore will not try to establish a connection to any chat services. This means you can effectively block StreamCore from establishing any chat connections for as long as you need until your class is ready.

### Example
```cs
using StreamCore;
using StreamCore.Chat;
using StreamCore.YouTube;
using StreamCore.Twitch;

namespace YourModsNamespace
{
    public class ChatMessageHandler : MonoBehaviour, ITwitchIntegration, IYouTubeIntegration 
    {
        public bool IsPluginReady { get; set; } = false;
        
        public void Awake()
        {
            // Setup chat message callbacks
            TwitchMessageHandlers.PRIVMSG += (twitchMsg) => {
                // do stuff with twitchMsg here
            };
            
            YouTubeMessageHandlers.OnMessageReceived += (youtubeMsg) => {
                // do stuff with youtubeMsg here
            };
            
            // Signal to StreamCore that this class is ready to receive chat callbacks
            IsPluginReady = true;
        }
    }
}
```


## StreamCore versions < 2.0 (Old/Legacy Version)
### Step 1
Initialize StreamCore in `OnApplicationStart`

*It's important that you base your client around calling `Initialize` in `OnApplicationStart`, to ensure you don't miss any callbacks if any other plugins call `Initialize` in `OnApplicationStart`.*

Make sure to include `StreamCore.Chat`:
```cs
using StreamCore.Chat;
```

Then call `Initialize` for the chat service you want to initialize. For now, since only Twitch is supported, we'll just initialize `TwitchWebSocketClient`.
```cs
public void OnApplicationStart()
{
  TwitchWebSocketClient.Initialize();
}
```

### Step 2
Subscribe to any callbacks you want to receive. You probably want to do this in `OnApplicationStart` as well.

*This can be time sensitive for callbacks such as `TwitchWebSocketClient.OnConnected`, as if you don't subscribe in time you might miss the callback. StreamCore will delay the connection after calling `Initialize` for 1 second, which should allow all plugins that utilize StreamCore to subscribe to these events in time.*

```cs
TwitchWebSocketClient.OnConnected += () => 
{
  Console.WriteLine("Connected to Twitch!");
}

TwitchMessageHandlers.PRIVMSG += (twitchMessage) => 
{
  Console.WriteLine($"Received PRIVMSG from {twitchMessage.user.displayName} in channel {twitchMessage.channelName}. Message: {twitchMessage.message}");
};
```

