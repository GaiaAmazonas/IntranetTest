export function requiresInitialTrainingVersion(versions:readonly unknown[]){
  return versions.length===0;
}

export function canDeleteTraining(trainingId:string,versions:readonly {trainingId:string}[]){
  return !versions.some(version=>version.trainingId===trainingId);
}
