
using System.Text.Json.Serialization;

public class PuzzlesPayload
{
	public PuzzleConfigPayload[] Puzzles;

	public PuzzlesPayload(List<PuzzleConfigPayload> puzzles)
	{
		Puzzles = puzzles.ToArray();
	}
}