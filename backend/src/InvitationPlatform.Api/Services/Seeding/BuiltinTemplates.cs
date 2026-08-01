namespace InvitationPlatform.Api.Services.Seeding;

/// <summary>One built-in template definition (name, description, and its default data JSON).</summary>
public record TemplateSeed(string Name, string Description, string Data);

/// <summary>
/// The catalogue of application-owned built-in templates. Adding a template here is all that is
/// needed for it to be seeded — the <see cref="DatabaseSeeder"/> inserts only the ones missing,
/// in a single existence query, so startup cost does not grow with the number of templates.
/// (The former "Classic Wedding" template was intentionally retired.)
/// </summary>
public static class BuiltinTemplates
{
    public static readonly IReadOnlyList<TemplateSeed> All = new[]
    {
        new TemplateSeed(
            "Elegant Noir",
            "Dark scrolling invitation with script typography, envelope opening, gallery, timeline and music",
            """
            {
              "title": "New Invitation",
              "cover": { "enabled": true, "eventLabel": "Wedding", "names": "Name & Name", "tagline": "Are getting married", "greeting": "Dear", "hostText": "", "image": "", "sealImage": "", "buttonText": "Tap to open" },
              "countdown": { "enabled": true, "label": "Save the date", "date": "", "description": "Venue name, City", "image": "" },
              "families": { "enabled": true, "label": "Together with their families", "title": "", "items": [] },
              "gallery": { "enabled": true, "label": "Before forever", "title": "A glimpse of us", "items": [] },
              "locations": { "enabled": true, "label": "Join us", "title": "The Celebration", "image": "", "items": [] },
              "timeline": { "enabled": true, "label": "The day", "title": "Wedding Timeline", "items": [] },
              "gifts": { "enabled": false, "label": "With love", "title": "Gift Registry", "description": "Your presence is the greatest gift. For those who wish, a wedding list is available:", "items": [] },
              "rsvp": { "enabled": true, "label": "Kindly reply by", "title": "Will you join us?", "deadline": "", "maxPeople": 10, "buttonText": "Send RSVP", "allowWishes": true },
              "memories": { "enabled": false, "title": "Share Your Memories", "description": "During or after the event, open the link below to share your photos with us", "url": "", "buttonText": "Share Memories" },
              "music": { "enabled": false, "url": "", "autoplay": true },
              "customSections": []
            }
            """),

        new TemplateSeed(
            "Serene Beige",
            "Light beige scrolling invitation with monogram hero, calendar card, split venue details, timeline, gallery and music",
            """
            {
              "title": "New Invitation",
              "cover": { "enabled": true, "eventLabel": "", "names": "Name & Name", "tagline": "Request the honor of your presence at their wedding", "hostIntro": "And the two shall become one", "hostOutro": "Mark 10: 8-9", "image": "", "buttonText": "" },
              "countdown": { "enabled": true, "label": "Save the date", "date": "", "description": "", "image": "" },
              "families": { "enabled": true, "label": "", "title": "", "items": [] },
              "locations": { "enabled": true, "label": "Where & When", "title": "", "image": "", "items": [] },
              "timeline": { "enabled": true, "label": "The day", "title": "Timeline", "items": [] },
              "gallery": { "enabled": true, "label": "", "title": "Captured Moments", "items": [] },
              "gifts": { "enabled": false, "label": "", "title": "Wedding Gift", "description": "Your presence is the best gift. Should you feel inclined, a list is available via Whish Money.", "items": [] },
              "rsvp": { "enabled": true, "label": "Be our guest", "title": "RSVP", "deadline": "", "maxPeople": 10, "buttonText": "Send Response", "allowWishes": true },
              "memories": { "enabled": false, "title": "Share Your Memories", "description": "During or after the event, open the link below to share your photos with us", "url": "", "buttonText": "Share Memories" },
              "music": { "enabled": false, "url": "", "autoplay": true },
              "customSections": []
            }
            """)
    };
}
