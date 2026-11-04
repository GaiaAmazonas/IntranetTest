import {describe,expect,it} from "vitest";
import {canDeleteTraining,requiresInitialTrainingVersion} from "./training-administration-rules";

describe("training administration onboarding",()=>{
  it("requires the first version when a newly created training has none",()=>{
    expect(requiresInitialTrainingVersion([])).toBe(true);
  });

  it("enables the regular management steps after the first version exists",()=>{
    expect(requiresInitialTrainingVersion([{}])).toBe(false);
  });
});

describe("training catalog deletion",()=>{
  it("allows deleting a training that has never had a version",()=>{
    expect(canDeleteTraining("training-1",[])).toBe(true);
  });

  it("preserves a training as soon as any version exists",()=>{
    expect(canDeleteTraining("training-1",[{trainingId:"training-1"}])).toBe(false);
  });
});
