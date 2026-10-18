using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>A few public-domain hymns so the library isn't empty on first run.</summary>
public static class SampleSongs
{
    public static IEnumerable<Song> All()
    {
        yield return Make("Amazing Grace", "John Newton (1779)", """
            [Verse 1]
            Amazing grace! How sweet the sound
            That saved a wretch like me!
            I once was lost, but now am found;
            Was blind, but now I see.

            [Verse 2]
            'Twas grace that taught my heart to fear,
            And grace my fears relieved;
            How precious did that grace appear
            The hour I first believed.

            [Verse 3]
            Through many dangers, toils and snares,
            I have already come;
            'Tis grace hath brought me safe thus far,
            And grace will lead me home.

            [Verse 4]
            The Lord has promised good to me,
            His Word my hope secures;
            He will my Shield and Portion be,
            As long as life endures.

            [Verse 5]
            When we've been there ten thousand years,
            Bright shining as the sun,
            We've no less days to sing God's praise
            Than when we'd first begun.
            """);

        yield return Make("Blessed Assurance", "Fanny Crosby (1873)", """
            [Verse 1]
            Blessed assurance, Jesus is mine!
            O what a foretaste of glory divine!
            Heir of salvation, purchase of God,
            Born of His Spirit, washed in His blood.

            [Chorus]
            This is my story, this is my song,
            Praising my Savior all the day long;
            This is my story, this is my song,
            Praising my Savior all the day long.

            [Verse 2]
            Perfect submission, perfect delight,
            Visions of rapture now burst on my sight;
            Angels descending bring from above
            Echoes of mercy, whispers of love.

            [Chorus]
            This is my story, this is my song,
            Praising my Savior all the day long;
            This is my story, this is my song,
            Praising my Savior all the day long.

            [Verse 3]
            Perfect submission, all is at rest,
            I in my Savior am happy and blest,
            Watching and waiting, looking above,
            Filled with His goodness, lost in His love.

            [Chorus]
            This is my story, this is my song,
            Praising my Savior all the day long;
            This is my story, this is my song,
            Praising my Savior all the day long.
            """);

        yield return Make("Holy, Holy, Holy", "Reginald Heber (1826)", """
            [Verse 1]
            Holy, holy, holy! Lord God Almighty!
            Early in the morning our song shall rise to Thee;
            Holy, holy, holy, merciful and mighty!
            God in three Persons, blessed Trinity!

            [Verse 2]
            Holy, holy, holy! All the saints adore Thee,
            Casting down their golden crowns around the glassy sea;
            Cherubim and seraphim falling down before Thee,
            Who was, and is, and evermore shall be.

            [Verse 3]
            Holy, holy, holy! though the darkness hide Thee,
            Though the eye of sinful man Thy glory may not see;
            Only Thou art holy; there is none beside Thee,
            Perfect in power, in love, and purity.

            [Verse 4]
            Holy, holy, holy! Lord God Almighty!
            All Thy works shall praise Thy name, in earth, and sky, and sea;
            Holy, holy, holy; merciful and mighty!
            God in three Persons, blessed Trinity!
            """);
    }

    private static Song Make(string title, string author, string lyrics) => new()
    {
        Title = title,
        Author = author,
        Copyright = "Public domain",
        Sections = SongParser.Parse(lyrics),
    };
}
