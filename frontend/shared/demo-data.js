/*
 * Demo / preview invitations.
 *
 * Used for showing the product to event planners and prospective customers without touching the
 * database: open any template with "?demo=<template-key>" and it renders this sample data instead
 * of fetching an invitation. Because no slug is resolved, the RSVP form runs in preview mode and
 * never posts anything.
 *
 * IMAGES: all artwork is original, wedding-specific vector art bundled with the app under
 * frontend/assets/demo/. Nothing is fetched from a third-party photo service, so there is no
 * licensing or attribution requirement, no external request, and the demo can never render an
 * off-theme image. Swap the paths in `COUPLE` below to use your own licensed photography.
 */
(function () {
  'use strict';

  // Artwork lives in the repo (frontend/assets/demo/*.svg) rather than coming from a third-party
  // photo service. Keyword-based services returned off-theme results (landscapes, random colours),
  // so every demo image is now bundled, wedding-specific, and identical on every load. Being SVG,
  // it is a few KB, needs no external request and stays sharp on any screen.
  //
  // To use your own photography instead, drop the files in assets/demo/ and change the paths below.
  const art = name => `/assets/demo/${name}.svg`;

  const COUPLE = {
    // Portrait/landscape mix so galleries demonstrate real-world proportions.
    hero:      art('couple-silhouette'),
    couple1:   art('bouquet'),           // portrait
    couple2:   art('couple-silhouette'),
    couple3:   art('rings'),
    couple4:   art('cake'),
    venue:     art('table-setting'),
    church:    art('chapel'),
    reception: art('table-setting'),
    gifts:     art('petals'),
    rsvpBg:    art('floral-arch')
  };

  const EVENT_DATE = '2027-06-12';

  const LOCATIONS = [
    {
      label: 'The Ceremony', time: '4:30 PM',
      name: 'Saint Elias Cathedral',
      addr: 'Rue Sursock, Achrafieh, Beirut',
      url: 'https://www.google.com/maps/search/?api=1&query=Beirut',
      img: COUPLE.church
    },
    {
      label: 'The Reception', time: '7:30 PM',
      name: 'Villa Rosa Gardens',
      addr: 'Broummana, Mount Lebanon',
      url: 'https://www.google.com/maps/search/?api=1&query=Broummana',
      img: COUPLE.reception
    }
  ];

  const TIMELINE = [
    { time: '4:30 PM', icon: '⛪', title: 'Ceremony',        subtitle: 'Saint Elias Cathedral', url: 'https://www.google.com/maps/search/?api=1&query=Beirut' },
    { time: '6:00 PM', icon: '📸', title: 'Photographs',     subtitle: 'Cathedral gardens' },
    { time: '7:30 PM', icon: '🥂', title: 'Cocktail Hour',   subtitle: 'Villa Rosa terrace', url: 'https://www.google.com/maps/search/?api=1&query=Broummana' },
    { time: '9:00 PM', icon: '🍽️', title: 'Dinner & Toasts', subtitle: 'The Orangery' },
    { time: '10:30 PM', icon: '💃', title: 'Dancing',        subtitle: 'Until late' }
  ];

  const FAMILIES = [
    { label: 'Parents of the Bride', names: 'Mr. & Mrs. Antoine Khoury' },
    { label: 'Parents of the Groom', names: 'Mr. & Mrs. Georges Haddad' }
  ];

  const GALLERY = [
    { url: COUPLE.couple1, caption: 'The proposal' },
    { url: COUPLE.couple2, caption: 'Summer in Batroun' },
    { url: COUPLE.couple3, caption: 'Engagement day' },
    { url: COUPLE.couple4, caption: 'Our happy place' }
  ];

  const RSVP = {
    enabled: true, label: 'Kindly reply by', title: 'Will You Join Us?',
    deadline: '2027-05-15', maxPeople: 4, buttonText: 'Send RSVP', allowWishes: true,
    image: COUPLE.rsvpBg,
    acceptMessage: 'Thank you! We are so happy you will be celebrating with us.\nSee you on the 12th of June.',
    declineMessage: 'Thank you for letting us know.\nYou will be dearly missed on our special day.'
  };

  const GIFTS = {
    enabled: true, label: 'With love', title: 'Gift Registry',
    image: COUPLE.gifts,
    description: 'Your presence is the greatest gift of all. For those who wish to contribute, a registry is available below.',
    items: [
      { bank: 'Bank of Beirut', account: 'LB00 1234 5678 9012 3456' },
      { bank: 'Whish Money',    account: '+961 71 234 567' }
    ]
  };

  const MUSIC = { enabled: false, url: '', autoplay: true };

  window.DEMO_INVITATIONS = {
    'elegant-noir': {
      title: 'Nadia & Karim',
      cover: {
        enabled: true, eventLabel: 'We Are Getting Married',
        names: 'Nadia & Karim', tagline: 'Together with our families, we invite you to share our joy',
        greeting: 'Dear', hostIntro: 'With hearts full of love,',
        hostText: 'invite you to celebrate the marriage of their children',
        hostOutro: '', image: COUPLE.hero, sealImage: '', buttonText: 'Tap to open'
      },
      countdown: { enabled: true, label: 'Save the date', date: EVENT_DATE, description: 'Beirut, Lebanon', image: COUPLE.venue },
      families:  { enabled: true, label: 'Together with their families', title: '', items: FAMILIES },
      gallery:   { enabled: true, label: 'Before forever', title: 'A Glimpse of Us', items: GALLERY },
      locations: { enabled: true, label: 'Join us', title: 'The Celebration', image: COUPLE.venue, items: LOCATIONS },
      timeline:  { enabled: true, label: 'The day', title: 'Wedding Timeline', items: TIMELINE },
      gifts:     GIFTS,
      rsvp:      RSVP,
      memories:  { enabled: true, title: 'Share Your Memories', description: 'During or after the celebration, upload your photos so we can relive the day through your eyes.', url: 'https://example.com/album', buttonText: 'Share Memories' },
      music:     MUSIC,
      customSections: [
        { enabled: true, label: '1 Corinthians 13:4', title: '', body: 'Love is patient, love is kind.\nIt always protects, always trusts,\nalways hopes, always perseveres.' }
      ]
    },

    'serene-beige': {
      title: 'Layla & Elias',
      cover: {
        enabled: true, eventLabel: '',
        names: 'Layla & Elias', tagline: 'Request the honour of your presence at their wedding',
        hostIntro: 'And the two shall become one', hostOutro: 'Mark 10: 8-9',
        image: COUPLE.hero, buttonText: ''
      },
      countdown: { enabled: true, label: 'Save the date', date: EVENT_DATE, description: 'Beirut, Lebanon', image: COUPLE.venue },
      families:  { enabled: true, label: '', title: '', items: FAMILIES },
      locations: { enabled: true, label: 'Where & When', title: '', image: COUPLE.venue, items: LOCATIONS },
      timeline:  { enabled: true, label: 'The day', title: 'Timeline', items: TIMELINE },
      gallery:   { enabled: true, label: '', title: 'Captured Moments', items: GALLERY },
      gifts:     GIFTS,
      rsvp:      Object.assign({}, RSVP, { label: 'Be our guest', title: 'RSVP', buttonText: 'Send Response' }),
      memories:  { enabled: true, title: 'Share Your Memories', description: 'Upload your photos from the day so we can relive it through your eyes.', url: 'https://example.com/album', buttonText: 'Share Memories' },
      music:     MUSIC,
      customSections: []
    }
  };

  /** Returns the demo payload for a "?demo=<key>" query, or null when not in demo mode. */
  window.getDemoData = function (search) {
    const raw = (search || window.location.search || '').replace(/^\?/, '').replace(/\?/g, '&');
    const key = new URLSearchParams(raw).get('demo');
    if (!key) return null;
    const data = window.DEMO_INVITATIONS[key.toLowerCase()];
    return data ? JSON.parse(JSON.stringify(data)) : null;
  };
})();
