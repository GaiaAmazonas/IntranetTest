import {describe,expect,it} from "vitest";
import {requiresInitialTrainingVersion} from "./training-administration-rules";

describe("training administration onboarding",()=>{
  it("requires the first version when a newly created training has none",()=>{
    expect(requiresInitialTrainingVersion([])).toBe(true);
  });

  it("enables the regular management steps after the first version exists",()=>{
    expect(requiresInitialTrainingVersion([{}])).toBe(false);
  });
});
