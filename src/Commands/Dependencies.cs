namespace WhMgr.Commands
{
    using System;

    using DSharpPlus.Interactivity;

    using WhMgr.Configuration;
    using WhMgr.Data.Subscriptions;
    using WhMgr.Localization;
    using WhMgr.Net.Webhooks;
    using WhMgr.Osm;

    public class Dependencies : IServiceProvider
    {
        //public InteractivityModule Interactivity;
        public InteractivityExtension Interactivity { get; }

        public WebhookController Whm { get; }

        public SubscriptionProcessor SubscriptionProcessor { get; }

        public WhConfig WhConfig { get; }

        public Translator Language { get; }

        public StripeService Stripe { get; }

        public OsmManager OsmManager { get; }

        public Dependencies(InteractivityExtension interactivity, WebhookController whm, SubscriptionProcessor subProcessor, WhConfig whConfig, Translator language, StripeService stripe)
        {
            Interactivity = interactivity;
            Whm = whm;
            SubscriptionProcessor = subProcessor;
            WhConfig = whConfig;
            Language = language;
            Stripe = stripe;
            OsmManager = new OsmManager();
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(WebhookController))
            {
                return Whm;
            }
            else if (serviceType == typeof(SubscriptionProcessor))
            {
                return SubscriptionProcessor;
            }
            else if (serviceType == typeof(WhConfig))
            {
                return WhConfig;
            }
            else if (serviceType == typeof(Translator))
            {
                return Language;
            }
            else if (serviceType == typeof(StripeService))
            {
                return Stripe;
            }
            else if (serviceType == typeof(OsmManager))
            {
                return OsmManager;
            }
            return null;
        }
    }
}