const genres = [
  "Horror", "Comedy", "Drama", "Sci-Fi", "Action/Adventure", "Animation/Anime",
  "Mystery/Thriller", "Romance", "Documentary", "Fantasy", "Crime", "War",
  "Western", "Musical", "Foreign/International", "Older Films", "Arthouse/Experimental"
];

const genreChoices = [
  { label: "Love it", signals: ["genre:{genre}=+2.0", "route:deep_module", "confidence:strong"] },
  { label: "Usually like it", signals: ["genre:{genre}=+1.0", "route:short_module", "confidence:medium"] },
  { label: "Mixed / depends", signals: ["genre:{genre}=0", "route:weak_spot_only", "confidence:light"] },
  { label: "Usually avoid it", signals: ["genre:{genre}=-1.5", "route:avoidance_probe", "confidence:medium"] },
  { label: "Almost never", signals: ["genre:{genre}=-2.5", "route:no_deep_module", "confidence:strong"] }
];

const sections = [
  {
    title: "Genre Calibration",
    tag: "core",
    questions: [{
      id: "GENRE_002",
      target: "careful genres",
      multi: true,
      question: "Pick up to five genres where the recommendation system should be especially careful.",
      choices: genres.map(g => ({ label: g, signals: [`careful_genre:${g}=true`] }))
    }]
  },
  {
    title: "Global Taste",
    tag: "core",
    questions: [
      q("GLOBAL_001", "pacing/payoff", "When a movie is slow, what usually makes it worth it for you?", [
        ["A strong emotional payoff", "boost:emotional-payoff +2", "condition:slow+payoff->boost", "confidence:strong"],
        ["A great ending or reveal", "boost:strong-ending +2", "boost:twist +1"],
        ["A unique mood or atmosphere", "boost:atmosphere +1.5", "boost:slow-burn +0.5"],
        ["Beautiful filmmaking even without much plot", "boost:formal-style +1.5", "boost:arthouse +0.5"],
        ["Slow movies usually lose me", "penalize:slow-burn -2", "penalize:low-plot -1.5"]
      ]),
      q("GLOBAL_002", "payoff", "How much do you need a movie to pay off its setup?", [
        ["I need a clear payoff", "boost:payoff +2", "penalize:no-payoff -2.5"],
        ["I like ambiguity if the emotional payoff is strong", "boost:emotional-payoff +2", "condition:ambiguity+emotional-payoff->boost"],
        ["I enjoy ambiguity and unresolved endings", "boost:ambiguity +2", "boost:opaque-ending +1"],
        ["It depends on the genre", "condition:genre-specific-payoff true", "confidence:light"]
      ]),
      q("GLOBAL_003", "intensity", "What kind of intensity works best for you?", [
        ["High tension with plot momentum", "boost:intense +2", "boost:plot-momentum +2"],
        ["Emotional intensity", "boost:emotional-intensity +2"],
        ["Creeping dread and quiet unease", "boost:quiet-dread +2"],
        ["I prefer restraint over intensity", "penalize:intense -1", "boost:restrained +1"]
      ]),
      q("GLOBAL_004", "story/form", "If a movie has a thin story, what can save it?", [
        ["Nothing; I need story", "penalize:thin-story -2.5", "boost:strong-story +2"],
        ["Original concept", "boost:original-idea +2", "condition:thin-story+original-idea->neutral"],
        ["Visual style", "boost:visual-style +2"],
        ["Mood and atmosphere", "boost:atmosphere +1.5"],
        ["Performance and dialogue", "boost:acting +1.5", "boost:dialogue +1"]
      ]),
      q("GLOBAL_005", "tone mismatch", "Which tone mismatch bothers you most?", [
        ["A serious movie that becomes silly", "penalize:serious-to-silly -2"],
        ["A fun movie that becomes too serious", "penalize:fun-to-bleak -1.5"],
        ["A scary movie that becomes goofy", "penalize:horror-comedy-mismatch -2"],
        ["A dramatic movie that becomes manipulative", "penalize:manipulative-drama -2"],
        ["Tone shifts are fine if they are intentional", "boost:tonal-risk +1"]
      ]),
      q("GLOBAL_006", "romance", "How do you usually feel about romance in movies?", [
        ["I like romance as the main story", "boost:romance-main +2"],
        ["I like romance as a subplot", "boost:romance-subplot +1.5", "penalize:romance-main -0.5"],
        ["Only if it is unusual or emotionally serious", "condition:romance+serious->boost", "penalize:generic-romance -1.5"],
        ["Romance usually lowers my interest", "penalize:romance-main -2", "penalize:romance-comedy -2"]
      ])
    ]
  },
  {
    title: "Horror",
    tag: "favorite genre weak spots",
    questions: [
      q("HORROR_001", "intense vs quiet dread", "When a horror movie is slow, quiet, and atmospheric, what makes it work for you?", [
        ["A strong ending or payoff", "condition:horror+quiet-dread+strong-ending->boost +2", "penalize:quiet-dread+no-payoff -2.5"],
        ["Sustained dread is enough", "boost:quiet-dread +2", "boost:slow-horror +1"],
        ["A fresh original idea", "boost:original-horror +2"],
        ["It usually does not work for me", "penalize:quiet-dread -2.5", "penalize:slow-horror -2"],
        ["Only if it stays scary, not just vague", "condition:quiet-dread+clear-threat->boost", "penalize:opaque-horror -2"]
      ]),
      q("HORROR_002", "payoff/arthouse", "How do you feel about art-house or opaque horror?", [
        ["I love it", "boost:arthouse-horror +2", "boost:opaque-horror +1.5"],
        ["I like it if there is emotional payoff", "condition:arthouse-horror+emotional-payoff->boost", "penalize:arthouse-horror+no-payoff -1.5"],
        ["I like the mood but often feel disappointed", "boost:atmosphere +1", "penalize:no-payoff -2"],
        ["I usually dislike it", "penalize:arthouse-horror -2.5", "penalize:opaque-horror -2"]
      ]),
      q("HORROR_003", "found footage/shaky camera", "How do found footage and shaky camera horror usually land for you?", [
        ["I like the realism", "boost:found-footage +2"],
        ["Only if the story is strong", "condition:found-footage+strong-story->boost", "penalize:found-footage+thin-story -2"],
        ["Shaky camera annoys me", "penalize:shaky-camera -2.5"],
        ["I usually avoid it", "penalize:found-footage -2"]
      ]),
      q("HORROR_004", "ingredients", "Which horror ingredients are positives for you?", [
        ["Intense dread and threat", "boost:intense-horror +2"],
        ["Supernatural mythology", "boost:supernatural +1.5"],
        ["Psychological unease", "boost:psychological-horror +1.5"],
        ["Gore and body horror", "boost:gore +1.5", "boost:body-horror +1.5"],
        ["Jump scares", "boost:jump-scares +1"],
        ["Horror comedy", "boost:horror-comedy +1.5"]
      ], true),
      q("HORROR_005", "weak spots", "Which horror pattern usually disappoints you?", [
        ["Quiet mood without payoff", "penalize:quiet-dread+no-payoff -3"],
        ["Too much shaky camera", "penalize:shaky-camera -2.5"],
        ["Too slow without plot", "penalize:slow-horror+thin-story -2.5"],
        ["Too silly or comedic", "penalize:horror-comedy -2"],
        ["Too much gore without tension", "penalize:gore+low-tension -2"]
      ], true)
    ]
  },
  module("Comedy", [
    q("COMEDY_001", "comedy subtypes", "Which kind of comedy works best for you?", [
      ["Action comedy", "boost:action-comedy +2"], ["Absurd or weird comedy", "boost:absurd-comedy +2", "boost:weird +1"], ["Dark comedy", "boost:dark-comedy +2"], ["Cringe comedy", "boost:cringe-comedy +1.5"], ["Romantic comedy", "boost:romance-comedy +2"]
    ], true),
    q("COMEDY_002", "comedy weak spots", "Which comedy style usually misses for you?", [
      ["Romantic comedy", "penalize:romance-comedy -2.5"], ["Cringe comedy", "penalize:cringe-comedy -2"], ["Broad silly comedy", "penalize:broad-comedy -2"], ["Random absurd comedy", "penalize:absurd-comedy -1.5"], ["Comedy that stops the plot", "penalize:low-plot-comedy -2"]
    ], true)
  ]),
  module("Drama", [
    q("DRAMA_001", "slow drama/payoff", "When a drama is slow, what do you need from it?", [
      ["Emotional payoff", "boost:emotional-payoff +2", "condition:slow-drama+emotional-payoff->boost"], ["Great acting and dialogue", "boost:acting +2", "boost:dialogue +1.5"], ["A clear story arc", "boost:story-arc +2", "penalize:plotless-drama -2"], ["Mood and realism are enough", "boost:realism +1.5", "boost:slow-drama +1"], ["Slow drama usually loses me", "penalize:slow-drama -2.5"]
    ]),
    q("DRAMA_002", "bleakness/ambiguity", "How much bleakness can a drama have before it becomes a problem?", [
      ["Bleak is fine if it feels meaningful", "condition:bleak+meaningful->boost", "boost:serious +1"], ["I need some hope or catharsis", "boost:catharsis +2", "penalize:bleak+no-catharsis -2"], ["I like uncompromising bleak movies", "boost:bleak +2"], ["I usually avoid bleak drama", "penalize:bleak -2.5"]
    ])
  ]),
  module("Sci-Fi", [
    q("SCIFI_001", "sci-fi subtype", "What kind of sci-fi are you most excited by?", [
      ["Big ideas and original concepts", "boost:idea-driven-scifi +2", "boost:original-idea +1.5"], ["Action sci-fi", "boost:action-scifi +2"], ["Puzzle, mystery, or twist sci-fi", "boost:puzzle-scifi +2", "boost:twist +1.5"], ["Philosophical slow sci-fi", "boost:philosophical-scifi +2", "boost:slow-scifi +1"], ["Alien worlds and world-building", "boost:world-building +2"]
    ], true),
    q("SCIFI_002", "sci-fi weak spots", "Which sci-fi problem bothers you most?", [
      ["Great concept, weak story", "penalize:concept-with-weak-story -2"], ["Too slow and philosophical", "penalize:slow-scifi -2"], ["Empty CGI spectacle", "penalize:empty-cgi -2.5"], ["Too much exposition", "penalize:exposition-heavy -1.5"], ["Predictable twist", "penalize:predictable-twist -2"]
    ], true)
  ]),
  module("Action / Adventure", [
    q("ACTION_001", "spectacle/stakes", "What makes action or adventure work for you?", [
      ["Kinetic practical action", "boost:kinetic-action +2", "boost:practical-stunts +1.5"], ["Emotional stakes", "boost:emotional-stakes +2"], ["Martial arts or fight choreography", "boost:martial-arts +2"], ["Large-scale spectacle", "boost:spectacle +1.5"], ["Fun adventure momentum", "boost:adventure-momentum +2"]
    ], true),
    q("ACTION_002", "weak spots", "Which action/adventure pattern lowers your interest?", [
      ["Empty CGI spectacle", "penalize:empty-cgi -2.5"], ["No emotional stakes", "penalize:no-stakes -2"], ["Generic superhero formula", "penalize:superhero-fatigue -2.5"], ["Too much quippy comedy", "penalize:quippy-action -1.5"], ["Messy editing", "penalize:messy-action -2"]
    ], true)
  ]),
  module("Animation / Anime", [
    q("ANIMATION_001", "animation taste", "What makes animation or anime appealing to you?", [
      ["Adult themes", "boost:adult-animation +2"], ["Visual imagination", "boost:visual-style +2", "boost:animation-craft +1.5"], ["Weird or surreal ideas", "boost:weird-animation +2"], ["Warm family tone", "boost:family-tone +1.5"], ["Action and world-building", "boost:anime-action +1.5", "boost:world-building +1.5"]
    ], true),
    q("ANIMATION_002", "animation weak spots", "What makes animation or anime less appealing to you?", [
      ["Too childish", "penalize:childish-tone -2"], ["Too loud or chaotic", "penalize:chaotic-animation -1.5"], ["Too sentimental", "penalize:sentimental -1.5"], ["Too visually generic", "penalize:generic-animation -1.5"], ["I rarely choose animation", "penalize:animation -2"]
    ], true)
  ]),
  module("Mystery / Thriller", [
    q("THRILLER_001", "twist/procedural/pacing", "What kind of mystery or thriller works best for you?", [
      ["A great twist", "boost:twist +2"], ["Procedural investigation", "boost:procedural +2"], ["Fast pacing and tension", "boost:tension +2", "boost:plot-momentum +1.5"], ["Ambiguous psychological mystery", "boost:psychological-thriller +2", "boost:ambiguity +1"], ["Crime atmosphere", "boost:crime-atmosphere +1.5"]
    ], true),
    q("THRILLER_002", "weak spots", "What ruins a mystery or thriller fastest?", [
      ["Predictable reveal", "penalize:predictable-twist -2.5"], ["Too slow", "penalize:slow-thriller -2"], ["No real suspense", "penalize:no-suspense -2.5"], ["Confusing instead of mysterious", "penalize:confusing-plot -2"], ["Unfair twist", "penalize:unfair-twist -2"]
    ], true)
  ]),
  module("Romance / Arthouse / Foreign / Older Films", [
    q("ROMANCE_001", "romance distinction", "When romance is present, what version works best?", [
      ["Romance as the main emotional engine", "boost:romance-main +2"], ["Romance as a subplot", "boost:romance-subplot +2"], ["Only tragic or serious romance", "boost:serious-romance +1.5", "penalize:light-romance -1.5"], ["Only if it does not take over the movie", "condition:romance-subplot->boost", "penalize:romance-main -2"], ["I usually dislike romance-driven movies", "penalize:romance-main -2.5"]
    ]),
    q("ARTHOUSE_001", "pacing/formal experimentation", "How do you feel about formally experimental or art-house movies?", [
      ["I love formal experimentation", "boost:formal-experimentation +2", "boost:arthouse +1.5"], ["I like it if the movie still has payoff", "condition:arthouse+payoff->boost", "penalize:arthouse+no-payoff -2"], ["I respect it but rarely enjoy it", "penalize:arthouse -1.5", "confidence:medium"], ["I usually dislike it", "penalize:arthouse -2.5", "penalize:opaque-storytelling -2"]
    ]),
    q("FOREIGN_001", "cultural distance/language", "How do you feel about international movies with unfamiliar cultural context?", [
      ["That makes them more interesting", "boost:foreign +1.5", "boost:cultural-specificity +1.5"], ["I like them if the story is accessible", "condition:foreign+accessible-story->boost"], ["I need strong genre hooks", "condition:foreign+genre-hook->boost", "penalize:foreign+slow -1.5"], ["They are harder for me to get into", "penalize:cultural-distance -1.5"]
    ]),
    q("OLD_001", "older films", "How do older movies usually work for you?", [
      ["I like classic pacing and style", "boost:older-films +2"], ["I like older movies if the story still moves", "condition:older-film+plot-momentum->boost", "penalize:older-film+slow -1.5"], ["Only major classics", "boost:canon-classic +1", "penalize:obscure-old-film -1.5"], ["Older pacing is usually a barrier", "penalize:older-films -2"]
    ])
  ], "cross-genre")
];

function q(id, target, question, choices, multi = false) {
  return { id, target, question, choices, multi };
}

function module(title, questions, tag = "module") {
  return { title, tag, questions };
}

function normalizeChoice(raw) {
  if (Array.isArray(raw)) {
    const [label, ...signals] = raw;
    return { label, signals };
  }
  return raw;
}

function render() {
  const form = document.getElementById("surveyForm");
  const genreSection = document.createElement("section");
  genreSection.innerHTML = `<div class="section-title"><h2>Genre Strength</h2><span class="tag">required foundation</span></div>`;
  const grid = document.createElement("div");
  grid.className = "genre-grid";
  genres.forEach((genre) => {
    const row = document.createElement("div");
    row.className = "genre-row";
    row.innerHTML = `<strong>${genre}</strong>`;
    const choices = document.createElement("div");
    choices.className = "choices";
    genreChoices.forEach((choice) => {
      const id = `GENRE_001_${genre}_${choice.label}`.replace(/[^a-z0-9]/gi, "_");
      const label = document.createElement("label");
      label.className = "choice";
      label.innerHTML = `<input type="radio" name="GENRE_001:${genre}" id="${id}" value="${choice.label}" data-signals="${choice.signals.map(s => s.replace("{genre}", genre)).join("|")}"><span>${choice.label}</span>`;
      choices.appendChild(label);
    });
    row.appendChild(choices);
    grid.appendChild(row);
  });
  genreSection.appendChild(grid);
  form.appendChild(genreSection);

  sections.forEach((section) => {
    const el = document.createElement("section");
    el.innerHTML = `<div class="section-title"><h2>${section.title}</h2><span class="tag">${section.tag}</span></div>`;
    section.questions.forEach((question) => {
      const item = document.createElement("div");
      item.className = "question";
      item.dataset.questionId = question.id;
      item.innerHTML = `<h3>${question.question}</h3>`;
      const choices = document.createElement("div");
      choices.className = "choices";
      question.choices.map(normalizeChoice).forEach((choice) => {
        const id = `${question.id}_${choice.label}`.replace(/[^a-z0-9]/gi, "_");
        const type = question.multi ? "checkbox" : "radio";
        const label = document.createElement("label");
        label.className = "choice";
        label.innerHTML = `<input type="${type}" name="${question.id}" id="${id}" value="${choice.label}" data-signals="${choice.signals.join("|")}"><span>${choice.label}</span>`;
        choices.appendChild(label);
      });
      item.appendChild(choices);
      el.appendChild(item);
    });
    form.appendChild(el);
  });

  updateProgress();
  form.addEventListener("change", updateProgress);
}

function collectAnswers() {
  const answers = [];
  const genreAnswers = [];
  genres.forEach((genre) => {
    const selected = document.querySelector(`input[name="GENRE_001:${CSS.escape(genre)}"]:checked`);
    if (selected) {
      genreAnswers.push({ genre, choice: selected.value, signals: selected.dataset.signals.split("|") });
    }
  });
  answers.push({ id: "GENRE_001", question: "How do you usually feel about these genres?", target: "genre strength", choices: genreAnswers });

  document.querySelectorAll(".question").forEach((item) => {
    const id = item.dataset.questionId;
    const selected = [...item.querySelectorAll("input:checked")].map(input => ({
      choice: input.value,
      signals: input.dataset.signals.split("|")
    }));
    answers.push({ id, question: item.querySelector("h3").textContent, target: findTarget(id), choices: selected });
  });

  return {
    survey: "taste-calibration",
    version: 1,
    submittedAt: new Date().toISOString(),
    answers,
    notes: document.getElementById("notes").value.trim()
  };
}

function findTarget(id) {
  for (const section of sections) {
    const question = section.questions.find(q => q.id === id);
    if (question) return question.target;
  }
  return "";
}

function collectTextReport() {
  const payload = collectAnswers();
  const lines = ["MovieDb Taste Calibration Survey", `Submitted: ${payload.submittedAt}`, ""];
  payload.answers.forEach((answer) => {
    lines.push(`[${answer.id}] ${answer.question}`);
    if (answer.id === "GENRE_001") {
      answer.choices.forEach((genreAnswer) => {
        lines.push(`- ${genreAnswer.genre}: ${genreAnswer.choice}`);
        genreAnswer.signals.forEach(signal => lines.push(`  signal: ${signal}`));
      });
    } else if (answer.choices.length === 0) {
      lines.push("- no answer");
    } else {
      answer.choices.forEach((choice) => {
        lines.push(`- ${choice.choice}`);
        choice.signals.forEach(signal => lines.push(`  signal: ${signal}`));
      });
    }
    lines.push("");
  });
  if (payload.notes) {
    lines.push("Notes", payload.notes);
  }
  return lines.join("\n");
}

function updateProgress() {
  const radioNames = new Set([...document.querySelectorAll('input[type="radio"]')].map(i => i.name));
  const checkboxGroups = new Set([...document.querySelectorAll('input[type="checkbox"]')].map(i => i.name));
  const total = radioNames.size + checkboxGroups.size;
  let done = 0;
  radioNames.forEach(name => { if (document.querySelector(`input[name="${CSS.escape(name)}"]:checked`)) done++; });
  checkboxGroups.forEach(name => { if (document.querySelector(`input[name="${CSS.escape(name)}"]:checked`)) done++; });
  document.getElementById("progress").value = total ? done / total : 0;
  document.getElementById("count").textContent = `${done} / ${total}`;
}

async function submitSurvey() {
  const status = document.getElementById("status");
  const payload = collectAnswers();
  status.textContent = "Saving...";
  status.className = "status";
  try {
    const response = await fetch("/api/taste-calibration/save", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ payload, text: collectTextReport() })
    });
    if (!response.ok) throw new Error(await response.text());
    const result = await response.json();
    status.textContent = `Saved: ${result.textPath}`;
    status.className = "status ok";
  } catch (error) {
    status.textContent = `Save failed. Use Download text file. ${error.message}`;
    status.className = "status error";
  }
}

function downloadSurvey() {
  download("taste-calibration-survey-results.json", JSON.stringify(collectAnswers(), null, 2), "application/json");
}

function downloadTextSurvey() {
  download("taste-calibration-survey-results.txt", collectTextReport(), "text/plain");
}

function download(filename, content, type) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

document.getElementById("submitButton").addEventListener("click", submitSurvey);
document.getElementById("downloadButton").addEventListener("click", downloadSurvey);
document.getElementById("downloadTextButton").addEventListener("click", downloadTextSurvey);
render();
