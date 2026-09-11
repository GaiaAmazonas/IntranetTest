import { describe, expect, it } from "vitest";
import { personInitials, profilePhotoPath } from "./person-avatar";

describe("PersonAvatar", () => {
  it("builds internal endpoints only from the authenticated profile or a person id", () => {
    expect(profilePhotoPath(undefined, true, 96)).toBe("/api/profile/photo?size=96");
    expect(profilePhotoPath("person id", false, 64)).toBe("/api/intranet/people/person%20id/photo?size=64");
    expect(profilePhotoPath(undefined, true, 34)).toBe("/api/profile/photo?size=48");
    expect(profilePhotoPath("person id", false, 192)).toBe("/api/intranet/people/person%20id/photo?size=240");
    expect(profilePhotoPath()).toBeNull();
  });

  it("keeps a stable initials fallback", () => {
    expect(personInitials("Edgar Eduardo Munar")).toBe("EE");
    expect(personInitials(" ")).toBe("GA");
  });
});
