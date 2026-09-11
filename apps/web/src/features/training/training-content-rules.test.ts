import { describe, expect, it } from "vitest";
import { youtubeEmbed } from "./training-content-rules";

describe("videos externos de capacitación", () => {
  it("normaliza enlaces comunes de YouTube", () => {
    expect(youtubeEmbed("https://www.youtube.com/watch?v=abcdefghijk")).toBe("https://www.youtube-nocookie.com/embed/abcdefghijk");
    expect(youtubeEmbed("https://youtu.be/abcdefghijk")).toBe("https://www.youtube-nocookie.com/embed/abcdefghijk");
    expect(youtubeEmbed("https://youtube.com/shorts/abcdefghijk")).toBe("https://www.youtube-nocookie.com/embed/abcdefghijk");
  });

  it("rechaza direcciones que no son de YouTube", () => {
    expect(youtubeEmbed("https://example.com/video.mp4")).toBeNull();
    expect(youtubeEmbed("texto cualquiera")).toBeNull();
  });
});
