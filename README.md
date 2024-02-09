## Visual Highlighting for Situated Brushing and Linking
This repository contains the Unity project used to conduct the user study found in the following publication:

> **Visual Highlighting for Situated Brushing and Linking**\
> By Nina Doerr, Benjamin Lee, Katarina Baricova, Dieter Schmalstieg, and Michael Sedlmair\
> Conditionally accepted in EuroVis 2024

The prototype was designed for use with an Oculus Quest 2 or Oculus Quest Pro in VR, running via Quest Link (cable or wireless). It has not been tested with any other devices.

> ⚠️ The original project and scene in the user study used a [paid supermarket asset](https://assetstore.unity.com/packages/3d/environments/modern-supermarket-186122). For copyright reasons, all relevant assets are instead replaced with simple shapes. We apologise for any inconvenience caused as this makes it much harder to (legally) re-use our prototype. ⚠️

### How to use
###### Running a user study
1. Open the *BasicScene* scene (or the *SupermarketScene* if the paid supermarket asset is installed)
2. Select the *StudyManager* in the hierarchy
3. Adjust its parameters, including the *Current Participant ID*, the *Participant Handedness*, and the *Folder Path* which the data is saved to
4. Enter play mode
5. In the same *StudyManager*, either click on *Start Study* to begin the study, or *Start Training* to begin a training session
6. The rest of the study is self-directed by the participant and automatically logged by the system. *StudyManager* provides additional buttons to start, stop, and skip trials, which can be used either for testing purposes or to quickly reset after system crashes

##### Adjusting tasks
The tasks that are given to participants are defined in the *Assets/UserStudy/TaskInfo.csv* file.

The text of the *Question* is taken verbatim from this file and presented directly to the participant.

The *Answer* is one or more names of GameObjects in the active scene that have the *Product* script attached to it. This name functionally serves as the ID of each product, and should be identical in this TaskInfo file, the active scene, and also the dataset of products (*Assets/StreamingAssets/DxRData/Products_Full.csv*).

The *Dimension1*, *Direction1*, *Value1*, etc. columns denote the thresholds which the visual cues for filtering should be placed. These make it easier for the participant to complete the filtering sections of the task.

##### Adjusting task order
The task order is determined by the specified participant ID and is defined in the *Assets/UserStudy/ParticipantInfo.csv* file.


### Acknowledgements
Development of the prototype was led by Benjamin Lee, with assistance from Nina Doerr and Katarina Baricova.

The prototype  uses several third-party toolkits and projects, including:
- [Oculus XR Plugins](https://assetstore.unity.com/publishers/25353)
- [DxR](https://github.com/ronellsicat/DxR)
- [Quick Outline](https://assetstore.unity.com/packages/tools/particles-effects/quick-outline-115488)
- [Immersive Visual Link](https://github.com/aprouzeau/ImmersiveVisualLink)
- [FlyingARrow](https://github.com/UweGruenefeld/OutOfView)
- [OneEuroFilter for Unity](https://github.com/jaantollander/OneEuroFilter)
