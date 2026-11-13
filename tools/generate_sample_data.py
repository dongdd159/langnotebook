#!/usr/bin/env python3
"""Generate a sample backup file for testing Language Notebook.

The output uses the same JSON format as Settings › Backup › Export, so it can be
loaded with Settings › Import backup (tick "Clear existing data" to start clean).

All dates are relative to the moment the script runs, so re-run it whenever you
want fresh "due today" cards, a current streak and a recent activity heatmap:

    python3 tools/generate_sample_data.py              # writes samples/sample-data.json
    python3 tools/generate_sample_data.py -o my.json   # custom output path
    python3 tools/generate_sample_data.py --seed 7     # different random history
"""
import argparse
import json
import random
from datetime import datetime, timedelta, timezone
from pathlib import Path

# Enum values as stored by the app (System.Text.Json writes enums as numbers).
VOCAB, GRAMMAR = 0, 1                                   # StudyItemKind
AGAIN, HARD, GOOD, EASY = 0, 1, 2, 3                    # ReviewGrade
BEGINNER, ELEMENTARY, INTERMEDIATE, UPPER, ADVANCED = 0, 1, 2, 3, 4  # ProficiencyLevel
DRAFT, FINISHED = 0, 1                                  # WritingStatus


def iso(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


# --------------------------------------------------------------------------- notebooks
NOTEBOOKS = [
    dict(Id=1, Name="Japanese N3", LanguageCode="ja", LanguageName="Japanese", ReadingLabel="Furigana",
         Description="Preparing for JLPT N3.", ColorHex="#c2571a", IsArchived=False),
    dict(Id=2, Name="English IELTS", LanguageCode="en", LanguageName="English", ReadingLabel="IPA",
         Description="Academic vocabulary and essay practice for IELTS 7.0.", ColorHex="#1f6b8f", IsArchived=False),
    dict(Id=3, Name="Korean Beginner", LanguageCode="ko", LanguageName="Korean", ReadingLabel="Romanization",
         Description="Everyday phrases and Hangul basics.", ColorHex="#6a43a8", IsArchived=False),
    dict(Id=4, Name="Spanish travel", LanguageCode="es", LanguageName="Spanish", ReadingLabel="Pronunciation",
         Description="Phrases from the last trip (archived).", ColorHex="#2f7a55", IsArchived=True),
]

# term, reading, meaning, part of speech, example, example meaning, collocations, synonyms, tags, level
VOCAB_DATA = {
    1: [
        ("勉強", "べんきょう", "study, learning", "n.", "毎日日本語を勉強しています。", "I study Japanese every day.", "勉強する\n勉強会", "学習", "study", ELEMENTARY),
        ("経験", "けいけん", "experience", "n.", "いい経験になりました。", "It was a good experience.", "経験する\n経験者", "体験", "work", INTERMEDIATE),
        ("準備", "じゅんび", "preparation", "n.", "旅行の準備はできた？", "Are you ready for the trip?", "準備する\n準備中", "用意", "travel", ELEMENTARY),
        ("会議", "かいぎ", "meeting, conference", "n.", "午後に会議があります。", "There is a meeting in the afternoon.", "会議室\n会議に出る", "ミーティング", "work", INTERMEDIATE),
        ("約束", "やくそく", "promise, appointment", "n.", "約束を守ってください。", "Please keep your promise.", "約束する\n約束を破る", "", "daily", ELEMENTARY),
        ("比べる", "くらべる", "to compare", "v.", "去年と比べると暑い。", "It is hot compared to last year.", "AとBを比べる", "比較する", "", INTERMEDIATE),
        ("届ける", "とどける", "to deliver", "v.", "荷物を届けました。", "I delivered the package.", "", "配達する", "daily", INTERMEDIATE),
        ("偶然", "ぐうぜん", "by chance", "adv.", "駅で偶然先生に会った。", "I ran into my teacher at the station.", "", "たまたま", "", INTERMEDIATE),
        ("苦手", "にがて", "weak at, dislike", "adj.", "数学が苦手です。", "I'm bad at maths.", "", "下手", "daily", ELEMENTARY),
        ("申し込む", "もうしこむ", "to apply", "v.", "試験に申し込みました。", "I applied for the exam.", "", "応募する", "study", INTERMEDIATE),
        ("締め切り", "しめきり", "deadline", "n.", "締め切りは金曜日です。", "The deadline is Friday.", "締め切りを守る", "期限", "work", INTERMEDIATE),
        ("混む", "こむ", "to be crowded", "v.", "電車が混んでいる。", "The train is crowded.", "", "", "travel", ELEMENTARY),
        ("必要", "ひつよう", "necessary", "adj.", "パスポートが必要です。", "A passport is necessary.", "", "", "travel", ELEMENTARY),
        ("確認", "かくにん", "confirmation, check", "n.", "メールを確認してください。", "Please check your email.", "確認する", "", "work", INTERMEDIATE),
        ("説明", "せつめい", "explanation", "n.", "もう一度説明してください。", "Please explain it again.", "説明する", "", "", ELEMENTARY),
        ("増える", "ふえる", "to increase", "v.", "外国人観光客が増えた。", "Foreign tourists have increased.", "", "", "", INTERMEDIATE),
        ("減らす", "へらす", "to reduce", "v.", "砂糖を減らしています。", "I'm cutting down on sugar.", "", "", "health", INTERMEDIATE),
        ("意見", "いけん", "opinion", "n.", "あなたの意見を聞かせてください。", "Please tell me your opinion.", "", "考え", "", ELEMENTARY),
        ("留守", "るす", "absence from home", "n.", "母は今留守です。", "My mother is out right now.", "", "", "daily", INTERMEDIATE),
        ("迷う", "まよう", "to get lost, to hesitate", "v.", "道に迷いました。", "I got lost.", "", "", "travel", INTERMEDIATE),
    ],
    2: [
        ("resilient", "/rɪˈzɪliənt/", "able to recover quickly", "adj.", "Children are often remarkably resilient.", "", "remarkably resilient\nresilient economy", "tough, adaptable", "IELTS,essay", UPPER),
        ("meticulous", "/məˈtɪkjələs/", "very careful and precise", "adj.", "She keeps meticulous notes.", "", "meticulous attention to detail", "thorough, precise", "IELTS", UPPER),
        ("contemplate", "/ˈkɒntəmpleɪt/", "to think about deeply", "v.", "He contemplated moving abroad.", "", "contemplate doing sth", "consider, ponder", "IELTS", UPPER),
        ("ubiquitous", "/juːˈbɪkwɪtəs/", "found everywhere", "adj.", "Smartphones are ubiquitous now.", "", "", "omnipresent, pervasive", "IELTS,essay", ADVANCED),
        ("mitigate", "/ˈmɪtɪɡeɪt/", "to make less severe", "v.", "Trees help mitigate air pollution.", "", "mitigate the effects of", "reduce, ease", "IELTS,essay", ADVANCED),
        ("congestion", "/kənˈdʒestʃən/", "heavy traffic", "n.", "Congestion is worse at rush hour.", "", "traffic congestion", "", "IELTS,environment", INTERMEDIATE),
        ("emissions", "/iˈmɪʃənz/", "gas released into the air", "n.", "Cars produce harmful emissions.", "", "carbon emissions\ncut emissions", "", "IELTS,environment", INTERMEDIATE),
        ("commute", "/kəˈmjuːt/", "to travel to work", "v.", "I commute by train.", "", "daily commute", "", "daily", INTERMEDIATE),
        ("infrastructure", "/ˈɪnfrəstrʌktʃə/", "basic systems of a country", "n.", "The city invested in infrastructure.", "", "transport infrastructure", "", "IELTS,environment", UPPER),
        ("pedestrian", "/pəˈdestriən/", "a person walking", "n.", "The street is for pedestrians only.", "", "pedestrian crossing", "walker", "IELTS", INTERMEDIATE),
        ("deadline", "/ˈdedlaɪn/", "a time limit", "n.", "We need to move the deadline to Friday.", "", "meet a deadline\nmiss a deadline", "due date", "work", ELEMENTARY),
        ("follow up", "/ˈfɒləʊ ʌp/", "to contact again later", "phr. v.", "I'll follow up with the client tomorrow.", "", "follow up on an email", "", "work,email", INTERMEDIATE),
        ("concise", "/kənˈsaɪs/", "short and clear", "adj.", "Please keep the summary concise.", "", "clear and concise", "brief, succinct", "writing", UPPER),
        ("ambiguous", "/æmˈbɪɡjuəs/", "having more than one meaning", "adj.", "The question was ambiguous.", "", "", "unclear, vague", "IELTS", UPPER),
        ("consensus", "/kənˈsensəs/", "general agreement", "n.", "There is a consensus among experts.", "", "reach a consensus", "agreement", "IELTS,essay", UPPER),
        ("detrimental", "/ˌdetrɪˈmentl/", "causing harm", "adj.", "Smoking is detrimental to health.", "", "detrimental effect on", "harmful", "IELTS,essay", ADVANCED),
        ("feasible", "/ˈfiːzəbl/", "possible to do", "adj.", "Is the plan financially feasible?", "", "technically feasible", "viable, practical", "IELTS", UPPER),
        ("negotiate", "/nɪˈɡəʊʃieɪt/", "to discuss to reach agreement", "v.", "They negotiated a new contract.", "", "negotiate a deal", "bargain", "work", INTERMEDIATE),
        ("reliable", "/rɪˈlaɪəbl/", "can be trusted", "adj.", "He is a reliable partner.", "", "reliable source", "dependable", "work", INTERMEDIATE),
        ("scrutinize", "/ˈskruːtənaɪz/", "to examine carefully", "v.", "Journalists scrutinized the report.", "", "", "examine, inspect", "IELTS", ADVANCED),
        ("alleviate", "/əˈliːvieɪt/", "to make pain or a problem less", "v.", "Better transport could alleviate traffic.", "", "alleviate poverty", "relieve, ease", "IELTS,essay", ADVANCED),
        ("on behalf of", "/ɒn bɪˈhɑːf əv/", "representing someone", "phrase", "I'm writing on behalf of the team.", "", "", "", "email", INTERMEDIATE),
        ("loneliness", "/ˈləʊnlinəs/", "being unhappy because alone", "n.", "Technology can increase loneliness.", "", "a sense of loneliness", "isolation", "IELTS,essay", INTERMEDIATE),
        ("sustainable", "/səˈsteɪnəbl/", "able to continue without harm", "adj.", "We need sustainable energy.", "", "sustainable development", "", "IELTS,environment", UPPER),
    ],
    3: [
        ("약속", "yaksok", "promise, appointment", "n.", "내일 친구랑 약속이 있어요.", "I have plans with a friend tomorrow.", "약속을 지키다", "", "daily", BEGINNER),
        ("도서관", "doseogwan", "library", "n.", "도서관에서 공부해요.", "I study at the library.", "도서관에 가다", "", "place", BEGINNER),
        ("아침", "achim", "morning; breakfast", "n.", "아침을 먹었어요.", "I ate breakfast.", "", "", "daily", BEGINNER),
        ("친구", "chingu", "friend", "n.", "친구를 만나요.", "I meet a friend.", "", "", "daily", BEGINNER),
        ("시간", "sigan", "time", "n.", "시간이 없어요.", "I don't have time.", "", "", "daily", BEGINNER),
        ("맛있다", "masitda", "to be delicious", "adj.", "이 음식은 맛있어요.", "This food is delicious.", "", "", "food", BEGINNER),
        ("비싸다", "bissada", "to be expensive", "adj.", "이 가방은 비싸요.", "This bag is expensive.", "", "", "shopping", BEGINNER),
        ("배우다", "baeuda", "to learn", "v.", "한국어를 배워요.", "I'm learning Korean.", "", "", "study", BEGINNER),
        ("지하철", "jihacheol", "subway", "n.", "지하철로 가요.", "I go by subway.", "", "", "travel", BEGINNER),
        ("날씨", "nalssi", "weather", "n.", "오늘 날씨가 좋아요.", "The weather is nice today.", "", "", "daily", BEGINNER),
        ("주말", "jumal", "weekend", "n.", "주말에 뭐 해요?", "What do you do at the weekend?", "", "", "daily", BEGINNER),
        ("병원", "byeongwon", "hospital", "n.", "병원에 가야 해요.", "I have to go to the hospital.", "", "", "place", ELEMENTARY),
    ],
    4: [
        ("la cuenta", "la KWEN-ta", "the bill", "n.", "La cuenta, por favor.", "The bill, please.", "", "", "restaurant", BEGINNER),
        ("¿Dónde está…?", "DON-de es-TA", "Where is…?", "phrase", "¿Dónde está la estación?", "Where is the station?", "", "", "travel", BEGINNER),
        ("el billete", "el bi-YE-te", "the ticket", "n.", "Un billete de ida y vuelta.", "A return ticket.", "", "", "travel", BEGINNER),
        ("barato", "ba-RA-to", "cheap", "adj.", "Es muy barato.", "It's very cheap.", "", "caro (antonym)", "shopping", BEGINNER),
    ],
}

# pattern, meaning, explanation, examples (one per line, "sentence = meaning"), common mistakes, topic, level, tags
GRAMMAR_DATA = {
    1: [
        ("〜たことがある", "have done (experience)", "Past tense of a verb + ことがある says you have had the experience at least once.",
         "日本に行ったことがあります。 = I have been to Japan.\n寿司を食べたことがない。 = I have never eaten sushi.",
         "Using the dictionary form (行くことがある) means “sometimes go”.", "Experience", ELEMENTARY, "N4"),
        ("〜ばかり", "nothing but; just did", "After nouns or て-form: only/always. After た-form: just finished.",
         "彼は文句ばかり言う。 = He does nothing but complain.\n今起きたばかりです。 = I just woke up.",
         "Confusing たばかり (subjective “just”) with たところ (exact timing).", "Particles", INTERMEDIATE, "N3"),
        ("〜ようにする", "make an effort to", "Verb dictionary/ない form + ようにする: try to make something a habit.",
         "毎日運動するようにしています。 = I try to exercise every day.",
         "Using ようになる (change of state) when you mean an effort.", "Habits", INTERMEDIATE, "N3"),
        ("〜わけではない", "it doesn't mean that", "Softly denies a conclusion the listener might draw.",
         "嫌いなわけではない。 = It's not that I dislike it.",
         "", "Nuance", UPPER, "N3"),
        ("〜おかげで", "thanks to", "Positive cause. Use せいで for a negative cause.",
         "先生のおかげで合格しました。 = I passed thanks to my teacher.",
         "Using おかげで for bad results — use せいで instead.", "Cause", INTERMEDIATE, "N3"),
    ],
    2: [
        ("If + S + had + V3, S + would have + V3", "Imagined past result that did not happen",
         "Third conditional. The if-clause can come first or second; use a comma only when it comes first.",
         "If I had studied, I would have passed. = I didn't study, so I failed.\nShe would have come if you had asked. = You didn't ask, so she didn't come.",
         "“If I would have known…” — never use would in the if-clause.", "Conditionals", UPPER, "essay"),
        ("S + would rather + V + than + V", "Prefer A to B", "Both verbs are bare infinitives. Short form: 'd rather.",
         "I would rather stay home than go out tonight. = I prefer staying home.",
         "Wrong: I would rather to stay. Right: I would rather stay.", "Preferences", INTERMEDIATE, "speaking"),
        ("Not only … but also …", "Adds emphasis to two points", "When Not only starts the sentence, invert subject and auxiliary.",
         "Not only is it cheap, but it is also fast. = It is cheap and fast.",
         "Forgetting inversion: “Not only it is cheap…”.", "Emphasis", UPPER, "essay"),
        ("It is widely believed that …", "Impersonal opinion phrase", "Useful in academic writing to present common views.",
         "It is widely believed that cars should be banned from city centres. = Many people think so.",
         "", "Academic phrases", INTERMEDIATE, "essay"),
        ("Despite + noun / V-ing", "Even though", "Followed by a noun or gerund, not a clause.",
         "Despite the rain, we went out. = Even though it rained, we went out.",
         "“Despite it rained” — use although for a clause.", "Contrast", INTERMEDIATE, "essay"),
    ],
    3: [
        ("-(으)ㄹ 거예요", "will, going to", "Attach to a verb stem to talk about plans or guesses.",
         "내일 도서관에 갈 거예요. = I'm going to the library tomorrow.\n비가 올 거예요. = It will probably rain.",
         "Dropping 으 after a consonant stem: 먹을 거예요, not 먹ㄹ 거예요.", "Tense", BEGINNER, "basics"),
        ("-고 싶다", "want to", "Verb stem + 고 싶다 for the speaker's wishes.",
         "한국에 가고 싶어요. = I want to go to Korea.",
         "Using 싶다 for a third person — use 고 싶어하다.", "Desire", BEGINNER, "basics"),
        ("-아/어서", "because; and then", "Connects reason or sequence; cannot be used with imperatives.",
         "배가 고파서 밥을 먹었어요. = I was hungry, so I ate.",
         "Using 아서 with commands: use (으)니까 instead.", "Connectors", ELEMENTARY, "basics"),
    ],
}

PROMPTS = [
    (2, "Should cities ban cars?", "Some people think private cars should be banned from city centres. Do you agree or disagree? Give reasons and examples.",
     "Environment", UPPER, 250, "congestion,emissions,pedestrian,commute,infrastructure"),
    (2, "Technology and loneliness", "Does technology make people more or less lonely? Discuss both views and give your opinion.",
     "Society", UPPER, 250, "loneliness,ubiquitous,detrimental,consensus"),
    (2, "Describe your ideal study space", "Where do you focus best? Describe the place, the time of day and why it works for you.",
     "Personal", INTERMEDIATE, 200, "meticulous,concise,reliable"),
    (1, "週末の予定", "今週末の予定について書いてください。誰と、どこで、何をしますか。", "Daily life", ELEMENTARY, 150, "予定,準備,約束"),
    (1, "私の苦手なこと", "苦手なことについて書いてください。どうやって克服していますか。", "Personal", INTERMEDIATE, 200, "苦手,経験,ようにする"),
    (3, "나의 하루", "오늘 하루에 대해 써 보세요. 무엇을 했어요? 기분이 어땠어요?", "Daily life", BEGINNER, 100, "아침,친구,-(으)ㄹ 거예요"),
]

CARS_ESSAY = (
    "Many large cities are struggling with congestion and poor air quality. Banning private cars from the centre "
    "is one radical answer, and I believe it is worth trying.\n\n"
    "First, fewer cars would mean lower emissions. Children walking to school would breathe cleaner air, and streets "
    "could be redesigned for every pedestrian rather than for traffic.\n\n"
    "However, a ban only works if people have another way to commute. Cities would need to invest in "
    "infrastructure such as frequent buses and safe cycle lanes before cars disappear.\n\n"
    "In conclusion, a car-free centre is a good idea, but only as part of a wider plan."
)

AI_CHANGES = [
    ("one radical answer", "one radical solution", "vocabulary",
     "“Answer” sounds informal here; “solution” is the usual word for fixing a problem like congestion."),
    ("for every pedestrian rather than for traffic", "for pedestrians rather than for traffic", "grammar",
     "Use a plural noun for people in general so it matches the general noun “traffic”."),
    ("a ban only works if", "a ban can only work if", "phrasing",
     "“Can only work” presents this as a condition, which sounds more measured in an essay."),
]


def words(text: str) -> int:
    return len(text.split())


def build(now: datetime, rng: random.Random) -> dict:
    today = now.replace(hour=0, minute=0, second=0, microsecond=0)

    # ---------------------------------------------------------------- vocabulary
    vocab, next_id = [], 1
    stage_plan = ["new", "learning", "young", "young", "mature", "mature", "new", "young", "mature", "learning"]
    for nb_id, rows in VOCAB_DATA.items():
        for i, (term, reading, meaning, pos, ex, exm, coll, syn, tags, level) in enumerate(rows):
            stage = stage_plan[(i + nb_id) % len(stage_plan)]
            created = now - timedelta(days=rng.randint(10, 80), hours=rng.randint(0, 23))
            if stage == "new":
                reps, interval, ease, lapses, last, due = 0, 0, 2.5, 0, None, created
            else:
                reps = {"learning": rng.randint(1, 2), "young": rng.randint(3, 5), "mature": rng.randint(6, 10)}[stage]
                interval = {"learning": 0, "young": rng.randint(2, 18), "mature": rng.randint(22, 60)}[stage]
                ease = round(rng.uniform(2.1, 2.8), 2)
                lapses = rng.choice([0, 0, 0, 1, 2]) if stage != "mature" else rng.choice([0, 0, 1])
                last = now - timedelta(days=rng.randint(0, max(1, interval)), hours=rng.randint(1, 10))
                # Roughly a third of reviewed cards are due now so the review queue is not empty.
                due = now - timedelta(hours=rng.randint(1, 30)) if rng.random() < 0.35 else last + timedelta(days=max(interval, 1))
            vocab.append(dict(
                Id=next_id, NotebookId=nb_id, Term=term, Reading=reading, Meaning=meaning, PartOfSpeech=pos,
                Example=ex, ExampleMeaning=exm, Collocations=coll, Synonyms=syn, Level=level, Tags=tags,
                Note="Pairs with “remarkably”." if term == "resilient" else "",
                IsFavorite=(i % 6 == 1), IsSuspended=(nb_id == 2 and term == "on behalf of"),
                EaseFactor=ease, IntervalDays=interval, Repetitions=reps, Lapses=lapses,
                LastReviewedUtc=iso(last) if last else None, DueUtc=iso(due),
                CreatedUtc=iso(created), UpdatedUtc=iso(last or created)))
            next_id += 1

    # ---------------------------------------------------------------- grammar
    grammar, next_id = [], 1
    for nb_id, rows in GRAMMAR_DATA.items():
        for i, (pattern, meaning, expl, examples, mistakes, topic, level, tags) in enumerate(rows):
            created = now - timedelta(days=rng.randint(10, 70))
            reviewed = i % 3 != 2
            interval = rng.randint(1, 30) if reviewed else 0
            last = now - timedelta(days=rng.randint(0, 10)) if reviewed else None
            due = (now - timedelta(hours=rng.randint(1, 20))) if (reviewed and i % 2 == 0) else (
                last + timedelta(days=interval) if reviewed else created)
            grammar.append(dict(
                Id=next_id, NotebookId=nb_id, Pattern=pattern, Meaning=meaning, Explanation=expl, Examples=examples,
                CommonMistakes=mistakes, Topic=topic, Level=level, Tags=tags, Note="", IsFavorite=False,
                IsSuspended=False, EaseFactor=2.5 if not reviewed else round(rng.uniform(2.2, 2.7), 2),
                IntervalDays=interval, Repetitions=rng.randint(2, 7) if reviewed else 0, Lapses=0,
                LastReviewedUtc=iso(last) if last else None, DueUtc=iso(due),
                CreatedUtc=iso(created), UpdatedUtc=iso(last or created)))
            next_id += 1

    # ---------------------------------------------------------------- prompts
    prompts = []
    for i, (nb_id, title, text, topic, level, target, terms) in enumerate(PROMPTS, start=1):
        prompts.append(dict(Id=i, NotebookId=nb_id, Title=title, PromptText=text, Topic=topic, Level=level,
                            TargetWordCount=target, SuggestedTerms=terms,
                            CreatedUtc=iso(now - timedelta(days=60 - i))))

    # ---------------------------------------------------------------- writing pieces
    ai_review = dict(
        CreatedUtc=iso(now - timedelta(days=1, hours=2)),
        SourceText=CARS_ESSAY,
        Summary="A clear position with relevant examples. The argument is easy to follow, but a few word choices are informal for an essay.",
        Criteria=[
            dict(Name="Task fulfillment", Score=19, Comment="Takes a clear position and supports it with examples."),
            dict(Name="Coherence & layout", Score=18, Comment="Logical paragraphs; add linking phrases between them."),
            dict(Name="Vocabulary", Score=20, Comment="Good topic words. Avoid informal choices like “answer”."),
            dict(Name="Grammar", Score=17, Comment="Mostly accurate. Check agreement in “every pedestrian … for traffic”."),
        ],
        Strengths=["States a clear opinion in the first paragraph.",
                   "Uses topic vocabulary naturally: congestion, emissions, commute.",
                   "Each paragraph has one main idea."],
        Improvements=["Develop the conclusion with a final recommendation.",
                      "Link paragraphs with phrases such as “As a result”.",
                      "Use more formal word choices for an argumentative essay."],
        Changes=[dict(Start=CARS_ESSAY.index(orig), Original=orig, Improved=imp, Category=cat, Explanation=why, Decision=0)
                 for orig, imp, cat, why in AI_CHANGES],
        UnmatchedChanges=0,
    )
    pieces_src = [
        (2, "Should cities ban cars?", CARS_ESSAY, FINISHED, 250, 74, (7, 6, 8, 7), "Use more linking words.", 1, ai_review),
        (2, "My favourite place to study",
         "My favourite place to study is a small library near my home. It is quiet in the morning and the desks face a "
         "large window, so I can concentrate for hours. I usually arrive at eight, put my phone in my bag and start with "
         "vocabulary review. The routine helps me stay focused and makes studying feel less like a chore.",
         FINISHED, 200, 84, (8, 8, 8, 8), "Good structure.", 2, None),
        (2, "Technology and loneliness",
         "Smartphones are ubiquitous, yet many people say they feel lonely. Some argue that social media connects us, "
         "while others believe it replaces real conversations. In my view, technology is not detrimental in itself; "
         "the problem is how we use it.",
         DRAFT, 250, 0, (0, 0, 0, 0), "", 4, None),
        (1, "週末の予定について",
         "今週末は友達と京都に行く予定です。朝早く起きて、電車で行きます。お寺を見たり、抹茶を飲んだりしたいです。準備はもうできました。",
         FINISHED, 150, 72, (7, 7, 6, 7), "助詞に注意。", 3, None),
        (1, "私の苦手なこと", "私は人前で話すのが苦手です。でも、毎日少しずつ練習するようにしています。", DRAFT, 200, 0, (0, 0, 0, 0), "", 6, None),
        (3, "나의 하루", "아침에 친구를 만났어요. 같이 도서관에서 공부했어요. 내일도 갈 거예요.", FINISHED, 100, 58, (6, 6, 5, 6), "", 9, None),
        (2, "A city I would like to visit",
         "I would like to visit Kyoto because of its temples and quiet streets. I have never been to Japan, but my "
         "friends say the autumn colours are beautiful.",
         FINISHED, 150, 66, (6, 7, 6, 6), "", 16, None),
    ]
    pieces = []
    for i, (nb_id, title, content, status, target, score, rubric, notes, days_ago, review) in enumerate(pieces_src, start=1):
        updated = now - timedelta(days=days_ago, hours=rng.randint(1, 8))
        pieces.append(dict(
            Id=i, NotebookId=nb_id, PromptId=None, Title=title, Content=content, Status=status,
            WordCount=words(content), TargetWordCount=target, DurationSeconds=rng.randint(600, 2400),
            AutoScore=score, ScoreTask=rubric[0], ScoreCoherence=rubric[1], ScoreVocabulary=rubric[2],
            ScoreGrammar=rubric[3], SelfNotes=notes, UsedTerms="",
            AiReviewJson=json.dumps(review, ensure_ascii=False) if review else "",
            CreatedUtc=iso(updated - timedelta(days=1)), UpdatedUtc=iso(updated)))

    # ---------------------------------------------------------------- review history (12 weeks)
    logs, log_id = [], 1
    items = [(VOCAB, v["Id"], v["NotebookId"]) for v in vocab if v["Repetitions"] > 0] + \
            [(GRAMMAR, g["Id"], g["NotebookId"]) for g in grammar if g["Repetitions"] > 0]
    streak_days = 12          # current streak ending today
    for day_offset in range(84, -1, -1):
        day = today - timedelta(days=day_offset)
        if day_offset < streak_days:
            count = 18 if day_offset == 0 else rng.randint(15, 42)       # 18 of 30 today
        elif day_offset == streak_days:
            count = 0                                                     # gap before the streak
        else:
            count = 0 if rng.random() < 0.3 else rng.randint(5, 45)
        for _ in range(count):
            kind, item_id, nb_id = rng.choice(items)
            grade = rng.choices([AGAIN, HARD, GOOD, EASY], weights=[8, 15, 58, 19])[0]
            reviewed = day + timedelta(hours=rng.randint(7, 22), minutes=rng.randint(0, 59))
            if reviewed > now:
                reviewed = now - timedelta(minutes=rng.randint(1, 120))
            logs.append(dict(
                Id=log_id, NotebookId=nb_id, ItemKind=kind, ItemId=item_id, Grade=grade, ReviewedUtc=iso(reviewed),
                IntervalAfterDays={AGAIN: 0, HARD: 1, GOOD: rng.randint(2, 10), EASY: rng.randint(5, 20)}[grade],
                EaseAfter=round(rng.uniform(2.0, 2.8), 2), DurationMs=rng.randint(2500, 12000)))
            log_id += 1

    return dict(
        Format="lang-notebook-backup", Version=1, ExportedUtc=iso(now),
        Notebooks=[dict(nb, CreatedUtc=iso(now - timedelta(days=90 - nb["Id"]))) for nb in NOTEBOOKS],
        VocabEntries=vocab, GrammarPatterns=grammar, WritingPrompts=prompts, WritingPieces=pieces, ReviewLogs=logs,
        Settings=dict(Id=1, DailyReviewGoal=30, NewCardsPerSession=10, MaxCardsPerSession=40, DefaultDirection=0,
                      ActiveNotebookId=None, WeeklyWritingGoal=1500, DisplayName="Learner"),
    )


def main() -> None:
    root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("-o", "--output", type=Path, default=root / "samples" / "sample-data.json")
    parser.add_argument("--seed", type=int, default=42, help="random seed for the review history")
    args = parser.parse_args()
    data = build(datetime.now(timezone.utc), random.Random(args.seed))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {args.output}")
    for key in ("Notebooks", "VocabEntries", "GrammarPatterns", "WritingPrompts", "WritingPieces", "ReviewLogs"):
        print(f"  {key}: {len(data[key])}")


if __name__ == "__main__":
    main()
