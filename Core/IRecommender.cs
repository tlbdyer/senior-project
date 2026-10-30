namespace Core;

// algorithm 2
public interface IRecommender
{
    // finished = false = skipped
    void Record(Track track, bool finished);

    // next track when the queue runs out, else null
    Track? Recommend(Track last, Library library, Random random);
}
