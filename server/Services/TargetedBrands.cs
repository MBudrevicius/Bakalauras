namespace server.Services;

/// <summary>
/// List of well-known brands commonly impersonated in phishing attacks.
/// Sources: Cloudflare top phished brands, Lithuanian market.
/// </summary>
public static class TargetedBrands
{
    public static readonly string[] All =
    [
        // Tech giants
        "google", "microsoft", "apple", "amazon", "meta", "oracle", "adobe", "salesforce", "dropbox", "slack", "zoom", "github", "gitlab", "samsung", "intel", "nvidia", "ibm", "cisco", "vmware", "dell", "lenovo", "hp", "asus", "acer",
        // Social media
        "facebook", "instagram", "twitter", "linkedin", "tiktok", "snapchat", "whatsapp", "telegram", "discord", "reddit", "pinterest", "youtube", "threads", "mastodon", "signal",
        // Email / cloud
        "outlook", "yahoo", "gmail", "icloud", "onedrive", "office365", "protonmail", "mailchimp", "sendgrid",
        // E-commerce
        "ebay", "alibaba", "aliexpress", "shopify", "walmart", "target", "bestbuy", "costco", "etsy", "wish", "shein", "temu",
        // Streaming / entertainment
        "netflix", "spotify", "steam", "twitch", "playstation", "xbox", "hulu", "disneyplus", "hbomax", "primevideo", "crunchyroll", "epicgames", "roblox",
        // Finance / banking
        "paypal", "chase", "wellsfargo", "bankofamerica", "citibank", "capitalone", "barclays", "hsbc", "revolut", "wise", "venmo", "cashapp", "stripe", "klarna", "santander", "ing", "bnp", "deutschebank", "creditsuisse", "monzo", "n26",
        // Crypto
        "coinbase", "binance", "kraken", "metamask", "blockchain", "opensea", "ledger", "trustwallet", "bybit", "okx", "kucoin", "phantom", "uniswap",
        // Shipping / logistics
        "dhl", "fedex", "ups", "usps", "royalmail", "dpd", "hermes", "gls", "postnord", "omniva",
        // Telecom
        "att", "verizon", "tmobile", "vodafone", "comcast", "orange", "telefonica", "ee",
        // Other frequently targeted
        "docusign", "wetransfer", "notion", "trello", "atlassian",  "godaddy", "namecheap", "cloudflare", "squarespace", "wix", "wordpress", "canva", "figma", "openai", "chatgpt",
        // Lithuanian banks
        "swedbank", "luminor", "seb", "siauliu", "citadele", "medicinosbankas",
        // Lithuanian telecom
        "telia", "tele2", "bite", "labas",
        // Lithuanian e-commerce / services
        "pigu", "varle", "barbora", "rimi", "maxima", "lidl", "iki", "senukai", "topocentras", "eurovaistine", "gintarine", "skypark", "autoplius", "aruodas", "skelbiu", "cvbankas",
        // Lithuanian government / public
        "sodra", "vmi", "epaslaugos", "registru", "epolicija", "elvysta", "emokejimai",
        // Lithuanian delivery / logistics
        "omniva", "dpd", "venipak", "lpexpress", "itella",
        // Lithuanian media / other
        "delfi", "lrytas", "fifteen", "lrt", "tv3", "paysera", "kevin", "montonio"
    ];
}
