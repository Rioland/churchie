#!/usr/bin/env python3
"""
Builds the Bible files that ship inside Churchie (src/Churchie.App/Bibles/*.json.gz).

Sources (all public domain or openly licensed - see THIRD-PARTY-NOTICES.md):
  * scrollmapper/bible_databases  formats/json/<NAME>.json   (KJV, BSB, ASV, BBE, YLT)
  * BibleNLP/ebible               corpus/<id>.txt + metadata/vref.txt  (WEB, Yoruba OYCB)

Usage:
  python3 convert.py <bible_databases checkout> <ebible checkout> <output folder>

Output format (Churchie JSON):
  { "abbreviation", "name", "language", "copyright",
    "books": [ { "number", "name", "chapters": [ ["verse 1", "verse 2", ...], ... ] } ] }
"""
import gzip
import json
import os
import re
import sys

# The 66 books in order with the codes eBible uses (vref.txt).
USFM_CODES = (
    "GEN EXO LEV NUM DEU JOS JDG RUT 1SA 2SA 1KI 2KI 1CH 2CH EZR NEH EST JOB PSA PRO ECC SNG "
    "ISA JER LAM EZK DAN HOS JOL AMO OBA JON MIC NAM HAB ZEP HAG ZEC MAL MAT MRK LUK JHN ACT "
    "ROM 1CO 2CO GAL EPH PHP COL 1TH 2TH 1TI 2TI TIT PHM HEB JAS 1PE 2PE 1JN 2JN 3JN JUD REV"
).split()

# Book names as printed in the Open Yoruba Contemporary Bible (eBible.org/yor).
YORUBA_BOOKS = [
    "Gẹnẹsisi", "Eksodu", "Lefitiku", "Numeri", "Deuteronomi", "Joṣua", "Onidajọ", "Rutu",
    "1 Samuẹli", "2 Samuẹli", "1 Ọba", "2 Ọba", "1 Kronika", "2 Kronika", "Esra", "Nehemiah",
    "Esteri", "Jobu", "Saamu", "Òwe", "Oniwaasu", "Orin Solomoni", "Isaiah", "Jeremiah",
    "Ẹkún Jeremiah", "Esekiẹli", "Daniẹli", "Hosea", "Joẹli", "Amosi", "Obadiah", "Jona", "Mika",
    "Nahumu", "Habakuku", "Sefaniah", "Hagai", "Sekariah", "Malaki", "Matiu", "Marku", "Luku",
    "Johanu", "Ìṣe àwọn Aposteli", "Romu", "1 Kọrinti", "2 Kọrinti", "Galatia", "Efesu", "Filipi",
    "Kolose", "1 Tẹsalonika", "2 Tẹsalonika", "1 Timotiu", "2 Timotiu", "Titu", "Filemoni",
    "Heberu", "Jakọbu", "1 Peteru", "2 Peteru", "1 Johanu", "2 Johanu", "3 Johanu", "Juda", "Ìfihàn",
]

ENGLISH_BOOKS = [
    "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua", "Judges", "Ruth",
    "1 Samuel", "2 Samuel", "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles", "Ezra",
    "Nehemiah", "Esther", "Job", "Psalms", "Proverbs", "Ecclesiastes", "Song of Solomon",
    "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel", "Hosea", "Joel", "Amos",
    "Obadiah", "Jonah", "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai", "Zechariah",
    "Malachi", "Matthew", "Mark", "Luke", "John", "Acts", "Romans", "1 Corinthians",
    "2 Corinthians", "Galatians", "Ephesians", "Philippians", "Colossians", "1 Thessalonians",
    "2 Thessalonians", "1 Timothy", "2 Timothy", "Titus", "Philemon", "Hebrews", "James",
    "1 Peter", "2 Peter", "1 John", "2 John", "3 John", "Jude", "Revelation",
]

SCROLLMAPPER = [
    # (file stem, output file, abbreviation, name, copyright)
    ("KJV", "en-kjv", "KJV", "King James Version (1769)", "Public domain."),
    ("BSB", "en-bsb", "BSB", "Berean Standard Bible",
     "The Berean Bible (www.berean.bible) Berean Standard Bible (BSB) is produced in cooperation with "
     "Bible Hub, Discovery Bible, OpenBible.com, and the Berean Bible Translation Committee. "
     "This text of God's Word has been dedicated to the public domain."),
    ("ASV", "en-asv", "ASV", "American Standard Version (1901)", "Public domain."),
    ("BBE", "en-bbe", "BBE", "Bible in Basic English (1949/1964)", "Public domain."),
    ("YLT", "en-ylt", "YLT", "Young's Literal Translation (1898)", "Public domain."),
]

EBIBLE = [
    # (corpus id, output file, abbreviation, name, language, book names, copyright)
    ("eng-engwebp", "en-web", "WEB", "World English Bible", "en", ENGLISH_BOOKS,
     "Public domain. \"World English Bible\" is a trademark of eBible.org."),
    ("yor-yor", "yo-oycb", "OYCB", "Bíbélì Mímọ́ Yorùbá Òde-Òní (Yoruba Contemporary Bible)", "yo", YORUBA_BOOKS,
     "Open Yoruba Contemporary Bible™ copyright © 2009, 2017 by Biblica, Inc. "
     "Original work available for free at www.biblica.com and open.bible. "
     "Licensed under Creative Commons Attribution-ShareAlike 4.0 (creativecommons.org/licenses/by-sa/4.0). "
     "Reformatted for Churchie from the eBible.org edition; the text is unchanged."),
]


def clean(text):
    return re.sub(r"\s+", " ", text).strip()


def write(out_dir, stem, data):
    raw = json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    path = os.path.join(out_dir, stem + ".json.gz")
    # mtime=0 keeps the file identical between runs (nicer for git)
    with open(path, "wb") as f, gzip.GzipFile(fileobj=f, mode="wb", compresslevel=9, mtime=0) as gz:
        gz.write(raw)
    return path


def from_scrollmapper(repo, out_dir):
    for stem, out, abbrev, name, copyright_ in SCROLLMAPPER:
        src = json.load(open(os.path.join(repo, "formats", "json", stem + ".json"), encoding="utf-8"))
        assert len(src["books"]) == 66, f"{stem}: expected 66 books"
        books = []
        for i, b in enumerate(src["books"]):
            chapters = []
            for c in b["chapters"]:
                verses = sorted(c["verses"], key=lambda v: v["verse"])
                assert [v["verse"] for v in verses] == list(range(1, len(verses) + 1)), (stem, b["name"], c["chapter"])
                chapters.append([clean(v["text"]) for v in verses])
            books.append({"number": i + 1, "name": ENGLISH_BOOKS[i], "chapters": chapters})
        data = {"abbreviation": abbrev, "name": name, "language": "en", "copyright": copyright_, "books": books}
        print("wrote", write(out_dir, out, data))


def from_ebible(repo, out_dir):
    vref = open(os.path.join(repo, "metadata", "vref.txt"), encoding="utf-8").read().splitlines()
    for corpus, out, abbrev, name, lang, names, copyright_ in EBIBLE:
        lines = open(os.path.join(repo, "corpus", corpus + ".txt"), encoding="utf-8").read().split("\n")
        # book -> chapter -> verse -> text
        tree = {}
        for ref, text in zip(vref, lines):
            code, cv = ref.split(" ")
            if code not in USFM_CODES:
                continue  # deuterocanonical books are not used
            chapter, verse = (int(x) for x in cv.split(":"))
            tree.setdefault(code, {}).setdefault(chapter, {})[verse] = clean(text)

        books = []
        for i, code in enumerate(USFM_CODES):
            chapters = []
            for chapter in range(1, max(tree[code]) + 1):
                verses = tree[code].get(chapter, {})
                count = max(verses) if verses else 0
                texts = [verses.get(v, "") for v in range(1, count + 1)]
                # drop verses this translation doesn't have at the end of a chapter
                while texts and not texts[-1]:
                    texts.pop()
                chapters.append(texts)
            books.append({"number": i + 1, "name": names[i], "chapters": chapters})
        data = {"abbreviation": abbrev, "name": name, "language": lang, "copyright": copyright_, "books": books}
        print("wrote", write(out_dir, out, data))


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    scrollmapper_repo, ebible_repo, output = sys.argv[1:]
    os.makedirs(output, exist_ok=True)
    from_scrollmapper(scrollmapper_repo, output)
    from_ebible(ebible_repo, output)
