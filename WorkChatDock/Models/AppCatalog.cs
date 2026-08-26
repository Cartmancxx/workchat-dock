namespace WorkChatDock.Models;

public static class AppCatalog
{
    public static List<AppDefinition> CreateDefaults() =>
    [
        App("zalo", "Zalo", "#1677FF", ["Zalo"], ["Zalo"], ["Zalo.exe"],
            ["%LOCALAPPDATA%\\Programs\\Zalo", "%APPDATA%\\ZaloData"]),
        App("dingtalk", "钉钉", "#2F7BFF", ["钉钉", "DingTalk", "DingDing"],
            ["DingTalk", "DingTalkLauncher"], ["DingTalkLauncher.exe", "DingTalk.exe"],
            ["%LOCALAPPDATA%\\DingTalk", "%ProgramFiles%\\DingTalk", "%ProgramFiles(x86)%\\DingTalk"]),
        App("feishu", "飞书 / Lark", "#27C58B", ["飞书", "Feishu", "Lark"],
            ["Feishu", "Lark"], ["Feishu.exe", "Lark.exe"],
            ["%LOCALAPPDATA%\\Feishu", "%LOCALAPPDATA%\\Lark", "%ProgramFiles%\\Feishu", "%ProgramFiles%\\Lark"]),
        App("jdme", "京ME", "#F53053", ["京ME", "JDME", "京东ME"], ["ME", "JDME"],
            ["ME.exe", "JDME.exe"], ["%LOCALAPPDATA%\\JDME", "%ProgramFiles%\\JDME"]),

        App("wechat", "微信", "#07C160", ["微信", "WeChat", "Weixin"], ["WeChat", "Weixin"],
            ["WeChat.exe", "Weixin.exe"],
            ["%ProgramFiles%\\Tencent\\WeChat", "%ProgramFiles(x86)%\\Tencent\\WeChat",
                "%ProgramFiles%\\Tencent\\Weixin", "%LOCALAPPDATA%\\Tencent\\WeChat"]),
        App("wecom", "企业微信 / WeCom", "#2B83F6", ["企业微信", "WeCom", "WXWork"], ["WXWork"],
            ["WXWork.exe"], ["%ProgramFiles%\\WXWork", "%ProgramFiles(x86)%\\WXWork", "%LOCALAPPDATA%\\WXWork"]),
        App("qq", "QQ", "#12B7F5", ["腾讯QQ", "QQ", "QQNT"], ["QQ"], ["QQ.exe"],
            ["%ProgramFiles%\\Tencent\\QQNT", "%ProgramFiles(x86)%\\Tencent\\QQNT", "%LOCALAPPDATA%\\Tencent\\QQ"]),
        App("tim", "TIM", "#2E8BFF", ["腾讯TIM", "TIM"], ["TIM"], ["TIM.exe"],
            ["%ProgramFiles%\\Tencent\\TIM", "%ProgramFiles(x86)%\\Tencent\\TIM"]),
        App("tencent-meeting", "腾讯会议", "#2D8CFF", ["腾讯会议", "Tencent Meeting", "WeMeet"],
            ["wemeetapp", "wemeet"], ["wemeetapp.exe", "wemeet.exe"],
            ["%APPDATA%\\Tencent\\WeMeet", "%LOCALAPPDATA%\\Tencent\\WeMeet", "%ProgramFiles%\\Tencent\\WeMeet"]),

        App("teams", "Microsoft Teams", "#6264A7", ["Microsoft Teams", "Teams", "MSTeams"],
            ["ms-teams", "Teams"], ["ms-teams.exe", "Teams.exe"],
            ["%LOCALAPPDATA%\\Microsoft\\Teams", "%LOCALAPPDATA%\\Microsoft\\WindowsApps",
                "%ProgramFiles%\\WindowsApps\\MSTeams*"]),
        App("slack", "Slack", "#611F69", ["Slack"], ["slack"], ["slack.exe"],
            ["%LOCALAPPDATA%\\slack", "%LOCALAPPDATA%\\Programs\\Slack"]),
        App("zoom", "Zoom Workplace", "#2D8CFF", ["Zoom", "Zoom Workplace"], ["Zoom", "Zoom Workplace"],
            ["Zoom.exe"], ["%APPDATA%\\Zoom", "%ProgramFiles%\\Zoom", "%ProgramFiles(x86)%\\Zoom"]),
        App("webex", "Cisco Webex", "#00BCEB", ["Cisco Webex", "Webex"], ["Webex", "CiscoCollabHost"],
            ["Webex.exe"], ["%LOCALAPPDATA%\\Programs\\Cisco Spark", "%ProgramFiles%\\Cisco Spark", "%ProgramFiles%\\Webex"]),
        App("outlook", "Microsoft Outlook", "#1473E6", ["Microsoft Outlook", "Outlook"], ["OUTLOOK", "olk"],
            ["OUTLOOK.EXE", "olk.exe"], ["%ProgramFiles%\\Microsoft Office", "%ProgramFiles(x86)%\\Microsoft Office",
                "%LOCALAPPDATA%\\Microsoft\\Olk"]),
        App("notion", "Notion", "#111111", ["Notion"], ["Notion"], ["Notion.exe"],
            ["%LOCALAPPDATA%\\Programs\\Notion", "%LOCALAPPDATA%\\Notion"]),

        App("telegram", "Telegram", "#2AABEE", ["Telegram", "Telegram Desktop"], ["Telegram"], ["Telegram.exe"],
            ["%APPDATA%\\Telegram Desktop", "%LOCALAPPDATA%\\Telegram Desktop"]),
        App("whatsapp", "WhatsApp", "#25D366", ["WhatsApp"], ["WhatsApp"], ["WhatsApp.exe"],
            ["%LOCALAPPDATA%\\WhatsApp", "%LOCALAPPDATA%\\Packages\\5319275A.WhatsAppDesktop*"]),
        App("signal", "Signal", "#3A76F0", ["Signal"], ["Signal"], ["Signal.exe"],
            ["%LOCALAPPDATA%\\Programs\\signal-desktop", "%LOCALAPPDATA%\\Signal"]),
        App("discord", "Discord", "#5865F2", ["Discord"], ["Discord"], ["Discord.exe"],
            ["%LOCALAPPDATA%\\Discord"]),
        App("line", "LINE", "#06C755", ["LINE"], ["LINE", "LineLauncher"], ["LINE.exe", "LineLauncher.exe"],
            ["%LOCALAPPDATA%\\LINE", "%ProgramFiles%\\LINE", "%ProgramFiles(x86)%\\LINE"]),
        App("viber", "Viber", "#7360F2", ["Viber"], ["Viber"], ["Viber.exe"],
            ["%LOCALAPPDATA%\\Viber", "%ProgramFiles%\\Viber"]),
        App("kakaotalk", "KakaoTalk", "#FEE500", ["KakaoTalk", "Kakao Talk"], ["KakaoTalk"], ["KakaoTalk.exe"],
            ["%ProgramFiles%\\Kakao\\KakaoTalk", "%ProgramFiles(x86)%\\Kakao\\KakaoTalk"]),
        App("mattermost", "Mattermost", "#0058CC", ["Mattermost"], ["Mattermost"], ["Mattermost.exe"],
            ["%LOCALAPPDATA%\\Programs\\mattermost-desktop", "%ProgramFiles%\\Mattermost"]),
        App("rocket-chat", "Rocket.Chat", "#F5455C", ["Rocket.Chat", "Rocket Chat"], ["Rocket.Chat", "RocketChat"],
            ["Rocket.Chat.exe", "RocketChat.exe"], ["%LOCALAPPDATA%\\Programs\\Rocket.Chat", "%ProgramFiles%\\Rocket.Chat"])
    ];

    private static AppDefinition App(
        string id,
        string displayName,
        string accentColor,
        string[] keywords,
        string[] processNames,
        string[] executableNames,
        string[] searchRoots) => new()
        {
            Id = id,
            DisplayName = displayName,
            AccentColor = accentColor,
            Keywords = keywords,
            ProcessNames = processNames,
            ExecutableNames = executableNames,
            NotificationNames = keywords,
            SearchRoots = searchRoots,
            Enabled = false,
            IsCustom = false
        };
}
