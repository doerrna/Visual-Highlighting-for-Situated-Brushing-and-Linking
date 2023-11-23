using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using DxR;
using System.IO;
using Random = System.Random;
using Oculus.Interaction.Input;

namespace BrushingAndLinking
{
    /// <summary>
    /// The StudyManager class is the main point of entry for everything associated with running the user study.
    ///
    /// All variables are exposed via the Inspector.
    /// </summary>
    public class StudyManager : MonoBehaviour
    {
        // Singleton pattern. The StudyManager can be accessed from anywhere using StudyManager.Instance
        public static StudyManager Instance { get; private set; }

        [Header("User Study Parameters")]
        [Min(1)] public int CurrentParticipantID = 1;
        public Handedness ParticipantHandedness = Handedness.Right;

        [Header("Data Logging")]
        public string FolderPath = "C:/Users/Benjamin/Desktop/Logs/";
        public bool LoggingEnabled = true;
        public bool LogInteractions = true;
        public bool NewFilePerParticipant = true;

        [Header("User Study Input Files")]
        public TextAsset ParticipantInfo;
        public TextAsset TaskInfo;

        [Header("Supermarket Variables")]
        public GameObject InLayoutProducts;
        public GameObject OutLayoutProducts;
        [Tooltip("Tutorials are the brief period of time where the participant can see the highlighting technique before the trial begins.")] public GameObject TutorialLayoutProducts;
        [Tooltip("Training is the stage before the user study begins, where the participant can try and practice with the interactions.")] public GameObject TrainingLayoutProducts;
        public Vis MainVis;
        public Tablet Tablet;
        public ButtonGroup XButtonGroup;
        public ButtonGroup YButtonGroup;

        [Header("Debug Parameters")]
        public bool AutoStartUserStudy = false;
        public bool AutoStartTraining = false;

        public List<StudyTrial> StudyTrials { get; private set; }
        public StudyTrial CurrentTrial { get { return StudyTrials[CurrentTrialIdx]; } }
        public int CurrentTrialIdx { get; private set; }

        public bool StudyActive { get; private set; }
        public bool TrialActive { get; private set; }
        public bool TrainingActive { get; private set; }

        // Data variables are using rows x columns (i.e. each list element is a row, each array element is a column within that row)
        private List<string[]> participantInfoData;
        private List<string[]> taskInfoData;
        private string[] participantInfoDataHeaders;
        private string[] taskInfoDataHeaders;

        private List<Product> studyProducts;

        private Random randGen;

        // Key is the configuration of independent variables, value is a list of questions associated with it
        private Dictionary<Tuple<ShelfLayout, TaskType>, List<StudyTask>> studyTasksDictionary;

        private StreamWriter mainDataStreamWriter;
        private StreamWriter interactionsDataStreamWriter;
        private TrialDataLog currentTrialDataLog;
        private float _trialStartTime;
        private float _trialFirstObjectSelectionTime;
        private float _trialLastTabletInteractionTime;

        private void Awake()
        {
            // Assign this object to the Instance property if it isn't already assigned, otherwise delete this object
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;

            ReadCsvFromString(ParticipantInfo.text, ref participantInfoData, ref participantInfoDataHeaders);
            ReadCsvFromString(TaskInfo.text, ref taskInfoData, ref taskInfoDataHeaders);
        }

        private void Start()
        {
            studyProducts = new List<Product>();
            studyProducts.AddRange(InLayoutProducts.GetComponentsInChildren<Product>());
            studyProducts.AddRange(OutLayoutProducts.GetComponentsInChildren<Product>());
            studyProducts.AddRange(TutorialLayoutProducts.GetComponentsInChildren<Product>());

            // Set handedness of all interactions
            Tablet.SetHandedness(ParticipantHandedness);
            FilterSliderManager.Instance.SetHandedness(ParticipantHandedness);
            BrushingOculusHandler.Instance.SetHandedness(ParticipantHandedness);

            SetProductsHidden();
            Tablet.SetOverallVisibility(false);

            if (AutoStartTraining)
            {
                StartTraining();
            }
            else if (AutoStartUserStudy)
            {
                StartStudy();
            }
        }

        public void StartTraining()
        {
            if (StudyActive || TrainingActive)
                return;

            TrainingActive = true;

            // Change the vis data set to a training one
            // TODO: Create training data set
            var json = MainVis.GetVisSpecs();
            json["data"]["url"] = "ProductData_Training.csv";
            MainVis.UpdateVisSpecsFromJSONNode(json);

            Tablet.SetOverallVisibility(true);
            Tablet.SetAlcoholButtonVisibility(true);

            // Show only training products
            SetProductsVisibility(ShelfLayout.Training);
            // Use the size highlighting for just the training layout
            foreach (Transform child in TrainingLayoutProducts.transform)
            {
                child.gameObject.GetComponent<Product>().SetHighlightTechnique(HighlightTechnique.Size);
            }

            Tablet.SetTaskText("<b>Traning Phase</b>\nPlease practice using the brushing and linking interactions.");

            // Show only the content on the tablet
            SetTabletAllVisibility(true, false, false, false, false, false);
        }

        public void StopTraining()
        {
            if (!TrainingActive)
                return;

            SetProductsHidden();
            Tablet.SetOverallVisibility(false);

            TrainingActive = false;
        }

        public void StartStudy()
        {
            if (StudyActive || TrainingActive)
                return;

            StudyActive = true;

            Tablet.SetOverallVisibility(true);

            // Load the tasks from the data set. We do this here because we destructively manipulate the task dictionaries which this function populates
            LoadTasks(ref taskInfoData);

            // Load the trials that are to be conudcted for this participant
            CalculateTrials(participantInfoData[CurrentParticipantID - 1]);
            CurrentTrialIdx = 0;

            // Set initial visibility rules
            SetProductsHidden();
            // Only show the pre-tutorial stuff
            SetTabletAllVisibility(false, true, false, false, false, false);

            // Load the first trial in the study
            LoadTrial(StudyTrials[CurrentTrialIdx]);

            // Initialise stream writer for data logging
            InitialiseDataLogging();
        }

        public void NextStudyStep()
        {
            if (!StudyActive)
                return;

            /// There are three possible states that the study can be in when moving to a next step
            /// 1. The loaded trial is not active and has not been completed -> Start the trial (i.e. pre-trial)
            /// 2. The loaded trial is currently active -> Stop the trial (i.e. mid-trial)
            /// 3. The loaded trial has already completed -> Load the next trial (i.e. post trial)

            // State 1: Start the trial
            if (!TrialActive && !CurrentTrial.IsTrialCompleted)
            {
                StartTrial();
            }
            // State 2: Stop the current trial
            else if (TrialActive)
            {
                StopTrial();
            }
            // State 3: Load the next trial
            else if (!TrialActive && CurrentTrial.IsTrialCompleted)
            {
                CurrentTrialIdx++;

                // If there are no more trials left, stop the study
                if (CurrentTrialIdx >= StudyTrials.Count)
                {
                    StopStudy();
                    return;
                }
                else
                {
                    LoadTrial(StudyTrials[CurrentTrialIdx]);
                }
            }
            else
            {
                throw new Exception("Error in NextStudyStep. This should not happen.");
            }
        }


        private void LoadTrial(StudyTrial trialToLoad)
        {
            // Change the vis data set
            var json = MainVis.GetVisSpecs();
            switch (trialToLoad.Layout)
            {
                case ShelfLayout.Tutorial:
                    json["data"]["url"] = "ProductData_Tutorial.csv";
                    Tablet.SetAlcoholButtonVisibility(false);
                    break;
                case ShelfLayout.In:
                    json["data"]["url"] = "ProductData_Inside.csv";
                    Tablet.SetAlcoholButtonVisibility(false);
                    break;
                case ShelfLayout.Out:
                    json["data"]["url"] = "ProductData_Outside.csv";
                    Tablet.SetAlcoholButtonVisibility(true);
                    break;
            }
            MainVis.UpdateVisSpecsFromJSONNode(json);

            // As a failsafe, reset all states across all brushing and linking mechanisms
            ResetAllBrushingAndLinking();
            // Hide all products at the start of the trial
            SetProductsHidden();
            // Set visibility rules of the tablet
            SetTabletAllVisibility(false,   // Always hide the vis, dimension change buttons, etc. when the trial is loaded
                                   trialToLoad.Task == TaskType.Tutorial,   // Load the pre-tutorial controls if the trial is a tutorial
                                   false,   // Don't show mid-tutorial controls
                                   trialToLoad.Task != TaskType.Tutorial,   // Load the pre-trial controls if the trial is a regular trial
                                   false,    // Don't load hypothesis response controls
                                   false     // Don't load post-trial controls
                                   );
            // Update the task text on the tablet
            switch (trialToLoad.Task)
            {
                case TaskType.Tutorial:
                    Tablet.SetTaskText(string.Format("<b>Tutorial for {0} technique</b>\nPlease spend a few minutes to practice and get used to the new highlighting technique.", CurrentTrial.Technique));
                    break;
                case TaskType.Single:
                    Tablet.SetTaskText(string.Format("<b>Single selection + {0} technique</b>\n{1}", trialToLoad.Technique, trialToLoad.QuestionText));
                    break;
                case TaskType.Multiple:
                    Tablet.SetTaskText(string.Format("<b>Multiple selection + {0} technique</b>\n{1}", trialToLoad.Technique, trialToLoad.QuestionText));
                    break;
                case TaskType.Hypothesis:
                    Tablet.SetTaskText(string.Format("<b>Statement question + {0} technique</b>\n{1}", trialToLoad.Technique, trialToLoad.QuestionText));
                    break;
            }
            // Set the highlighting technique on the products
            SetHighlightTechnique(trialToLoad.Technique);
        }

        public void StartTrial()
        {
            if (CurrentTrial.IsTrialCompleted || TrialActive)
                return;

            TrialActive = true;

            // Show the products
            SetProductsVisibility(CurrentTrial.Layout);
            // Set visibility rules on tablet
            SetTabletAllVisibility(true,    // Show vis, etc.
                                   false,   // Don't show pre-tutorial controls
                                   CurrentTrial.Task == TaskType.Tutorial,   // Load mid-tutorial controls if the trial is a tutorial
                                   false,   // Don't show pre-trial controls
                                   CurrentTrial.Task == TaskType.Hypothesis, // Load the response buttons if it is a hypothesis task
                                   false    // Don't show post-trial controls
                                   );

            // Provide visual cues for the appropriate dimensions to use depending on the given task
            if (CurrentTrial.Task != TaskType.Tutorial)
            {
                XButtonGroup.HighlightButtonByDimensionName(CurrentTrial.DimensionName1);
                XButtonGroup.HighlightButtonByDimensionName(CurrentTrial.DimensionName2);
                YButtonGroup.HighlightButtonByDimensionName(CurrentTrial.DimensionName1);
                YButtonGroup.HighlightButtonByDimensionName(CurrentTrial.DimensionName2);
                FilterSliderManager.Instance.TaskStarted();
            }

            // Start logging variables
            if (CurrentTrial.Task != TaskType.Tutorial)
            {
                currentTrialDataLog = new TrialDataLog();
                _trialStartTime = Time.time;
                _trialFirstObjectSelectionTime = -1;
                _trialLastTabletInteractionTime = -1;
            }
        }

        public void StopTrial()
        {
            if (!TrialActive)
                return;

            ResetAllBrushingAndLinking();
            // Hide the products
            SetProductsHidden();
            ResetHiddenProducts();
            // Set visibility rules on tablet
            SetTabletAllVisibility(false,   // Don't show vis, etc.
                                   false,   // Don't show pre-tutorial controls
                                   false,   // Don't show mid-tutorial controls
                                   false,   // Don't show pre-trial controls
                                   false,   // Don't show hypothesis response controls
                                   true     // Show post-trial controls
                                   );

            // Hide the visual cues for the dimension buttons
            if (CurrentTrial.Task != TaskType.Tutorial)
            {
                XButtonGroup.UnhighlightButtons();
                YButtonGroup.UnhighlightButtons();
            }

            // Mark this trial as completed
            CurrentTrial.TrialCompleted();

            // Stop logging. Save logging variables
            if (CurrentTrial.Task != TaskType.Tutorial)
            {
                currentTrialDataLog.CompletionTime = Time.time - _trialStartTime;
                currentTrialDataLog.TimeUntilLastTabletInteraction = (_trialLastTabletInteractionTime != -1) ? _trialLastTabletInteractionTime - _trialStartTime : -1;
                currentTrialDataLog.TimeUntilFirstObjectSelected = (_trialFirstObjectSelectionTime != -1) ? _trialFirstObjectSelectionTime - _trialStartTime : -1;
                currentTrialDataLog.CountOfSelectedObjects = currentTrialDataLog.SelectedObjectNames.Count;
                // For single and multiple task, the number of wrong objects is the number of selected objects minus the number of expected answers
                if (CurrentTrial.Task != TaskType.Hypothesis)
                {
                    currentTrialDataLog.CountOfWrongSelectedObjects = currentTrialDataLog.SelectedObjectNames.Count - CurrentTrial.QuestionAnswers.Length;
                }
                // For hypothesis task, the number of wrong objects is simply whether the answer was correct or not (1: incorrect, 0: correct)
                else
                {
                    // When testing we might just skip past this hypothesis response. This check just makes debugging easier
                    if (currentTrialDataLog.SelectedObjectNames.Count > 0)
                        currentTrialDataLog.CountOfWrongSelectedObjects = currentTrialDataLog.SelectedObjectNames[0].ToLower() == CurrentTrial.QuestionAnswers[0].ToLower() ? 0 : 1;
                }
                WriteDataLogging();
            }

            Tablet.SetTaskText("Please return to the middle of the room as indicated by the feet image. Press the button to the right when you have done so.");

            // Play a sound effect
            SoundEffectPlayer.Instance.PlayTrialFinished();

            TrialActive = false;
        }

        /// <summary>
        /// Skips a trial entirely. Used mainly for when something goes wrong and a later part of the study needs to be skipped to
        /// </summary>
        public void SkipTrial()
        {
            if (!TrialActive && !CurrentTrial.IsTrialCompleted)
            {
                CurrentTrialIdx++;
                LoadTrial(StudyTrials[CurrentTrialIdx]);
            }
        }

        public void StopStudy()
        {
            if (!StudyActive)
                return;

            StopDataLogging();

            Tablet.SetOverallVisibility(false);

            StudyActive = false;
        }


        #region Study configuration and loading methods

        private void ReadCsvFromString(string text, ref List<string[]> data, ref string[] headers)
        {
            // Split entire string
            string[] textSplit = text.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries);

            // Read header
            headers = textSplit[0].Split(',');

            // Create data structure
            data = new List<string[]>();

            // Read data line by line. Add each row to our list of lists
            for (int i = 1; i < textSplit.Length; i++)
            {
                string[] lineSplit = textSplit[i].Split(',');
                data.Add(lineSplit);
            }
        }

        private void LoadTasks(ref List<string[]> taskInfoData)
        {
            // Create data structure
            studyTasksDictionary = new Dictionary<Tuple<ShelfLayout, TaskType>, List<StudyTask>>();

            foreach (string[] row in taskInfoData)
            {
                Enum.TryParse(row[1], out ShelfLayout layout);
                Enum.TryParse(row[0], out TaskType task);

                // Get the list of study tasks (questions) associated with this configuration of independent variables, or create one if it does not yet exist
                Tuple<ShelfLayout, TaskType> key = new Tuple<ShelfLayout, TaskType>(layout, task);
                if (!studyTasksDictionary.TryGetValue(key, out List<StudyTask> taskList))
                {
                    taskList = new List<StudyTask>();
                    studyTasksDictionary.Add(key, taskList);
                }

                taskList.Add(new StudyTask(row[2], row[3], row[4].Split(';'), row[5], row[6], row[7], row[8], row[9], row[10]));
            }
        }


        private void CalculateTrials(string[] participantInfo)
        {
            StudyTrials = new List<StudyTrial>();

            // Set a random seed to be that of the participant ID. This ensures randomness between participants, but consistency of task order for each individual participant.
            // This is particularly useful if the prototype restarts mid-experiment, as the questions asked will be the same every time
            randGen = new Random(CurrentParticipantID);

            // This assumes a very fixed format for the input data. This function must be edited if the input format changes
            for (int i = 1; i < participantInfo.Length; i += 3)
            {
                Enum.TryParse(participantInfo[i], out HighlightTechnique technique);
                Enum.TryParse(participantInfo[i+1], out ShelfLayout layout1);
                Enum.TryParse(participantInfo[i+2], out ShelfLayout layout2);

                // Before each highlighting technique is always a brief tutorial stage
                StudyTrials.Add(new StudyTrial(technique, ShelfLayout.Tutorial, TaskType.Tutorial));

                // Now add the trials for our first shelf layout
                StudyTrials.Add(CreateStudyTrialFromIndependentVariables(technique, layout1, TaskType.Single));
                StudyTrials.Add(CreateStudyTrialFromIndependentVariables(technique, layout1, TaskType.Multiple));
                StudyTrials.Add(CreateStudyTrialFromIndependentVariables(technique, layout1, TaskType.Hypothesis));

                // Now do the same for our second shelf layout
                StudyTrials.Add(CreateStudyTrialFromIndependentVariables(technique, layout2, TaskType.Single));
                StudyTrials.Add(CreateStudyTrialFromIndependentVariables(technique, layout2, TaskType.Multiple));
                StudyTrials.Add(CreateStudyTrialFromIndependentVariables(technique, layout2, TaskType.Hypothesis));
            }

            // Assign trial numbers to each one (to make it easier to tell the number of trials done/to-go)
            for (int i = 0; i < StudyTrials.Count; i++)
            {
                StudyTrials[i].TrialNumber = i;
            }
        }

        private StudyTrial CreateStudyTrialFromIndependentVariables(HighlightTechnique technique, ShelfLayout layout, TaskType task)
        {
            // Get the list of questions for our specific configuration of independent variables
            Tuple<ShelfLayout, TaskType> key = new Tuple<ShelfLayout, TaskType>(layout, task);
            List<StudyTask> taskList = studyTasksDictionary[key];

            // Get a random task from the given task list
            int rand = randGen.Next(0, taskList.Count);
            StudyTask studyTask = taskList[rand];

            // Create new StudyTrial object
            StudyTrial retVal = new StudyTrial(technique, layout, task, studyTask);

            // Remove this task from our list of tasks
            taskList.RemoveAt(rand);

            return retVal;
        }


        private void SetProductsVisibility(ShelfLayout layout)
        {
            bool inVisibility = false;
            bool outVisibility = false;
            bool tutorialVisibility = false;
            bool trainingVisibility = false;

            switch (layout)
            {
                case ShelfLayout.In:
                    {
                        inVisibility = true;
                        break;
                    }

                case ShelfLayout.Out:
                    {
                        inVisibility = true;
                        outVisibility = true;
                        break;
                    }

                case ShelfLayout.Tutorial:
                    {
                        tutorialVisibility = true;
                        break;
                    }

                case ShelfLayout.Training:
                    {
                        trainingVisibility = true;
                        break;
                    }

                case ShelfLayout.None:
                    break;
            }

            InLayoutProducts.SetActive(inVisibility);
            OutLayoutProducts.SetActive(outVisibility);
            TutorialLayoutProducts.SetActive(tutorialVisibility);
            TrainingLayoutProducts.SetActive(trainingVisibility);

            HighlightManager.Instance.ResetProductReferences();
        }

        private void SetProductsHidden()
        {
            SetProductsVisibility(ShelfLayout.None);
        }

        private void SetTabletContentVisibility(bool visible)
        {
            Tablet.SetContentVisibility(visible);
        }

        private void SetTabletControlsVisibility(TabletControls tabletControls, bool visible)
        {
            Tablet.SetControlsVisibility(tabletControls, visible);
        }

        private void SetTabletAllVisibility(bool content, bool preTutorial, bool midTutorial, bool preTrial, bool hypothesisResponse, bool postTrial)
        {
            SetTabletContentVisibility(content);
            SetTabletControlsVisibility(TabletControls.PreTutorial, preTutorial);
            SetTabletControlsVisibility(TabletControls.MidTutorial, midTutorial);
            SetTabletControlsVisibility(TabletControls.PreTrial, preTrial);
            SetTabletControlsVisibility(TabletControls.HypothesisResponse, hypothesisResponse);
            SetTabletControlsVisibility(TabletControls.PostTrial, postTrial);
        }

        private void SetHighlightTechnique(HighlightTechnique technique)
        {
            foreach (Product product in studyProducts)
            {
                product.SetHighlightTechnique(technique);
            }
        }

        private void ResetAllBrushingAndLinking()
        {
            BrushingManager.Instance.RemoveAllBrushing();
            HighlightManager.Instance.UnhighlightAllProducts();
            // Reset the filter sliders too
            FilterSliderManager.Instance.ResetFiltering();

            // Make sure that the LinkHighlighter has cleared out its own internal dictionary. It will recreate it by itself
            LinkHighlighter.VisMarksChanged();
            // Clear the arrows as well
            ArrowHighlighter.VisMarksChanged();
        }

        #endregion Study configuration and loading methods

        #region Participant interaction methods

        public void InteractionOccurred(InteractionType interactionType, string comment = "")
        {
            _trialLastTabletInteractionTime = Time.time;

            // Log to more detailed dataset
            LogInteractionData(Time.time - _trialStartTime, interactionType, comment);
        }

        public void ProductSelected(Product product)
        {
            // For training, we still want to play a sound effect
            if (TrainingActive)
            {
                SoundEffectPlayer.Instance.PlayCorrectProductSelected();
                return;
            }

            if (!TrialActive)
                return;

            // We only care about selections if the task is either a single or multiple
            if (CurrentTrial.Task == TaskType.Single || CurrentTrial.Task == TaskType.Multiple)
            {
                // Add selected product to the log, regardless if it is right or wrong
                currentTrialDataLog.SelectedObjectNames.Add(product.gameObject.name);

                // Log the interaction to more detailed dataset
                // Note that this product selection doesn't count as a tablet interaction, but is still an interaction nonetheless
                LogInteractionData(Time.time - _trialStartTime, InteractionType.ProductSelect, product.gameObject.name);

                // If no value has been logged for the first selection yet, mark the time
                if (_trialFirstObjectSelectionTime == -1)
                {
                    _trialFirstObjectSelectionTime = Time.time;
                }

                // Check the chosen object against the list of answers to this task
                if (CurrentTrial.QuestionAnswers.Contains(product.gameObject.name))
                {
                    // Correct answer. Assign its response to true
                    CurrentTrial.QuestionResponses[Array.IndexOf(CurrentTrial.QuestionAnswers, product.name)] = true;

                    // Play a sound effect
                    SoundEffectPlayer.Instance.PlayCorrectProductSelected();

                    // If all question responses are true, we can finish the trial
                    if (CurrentTrial.QuestionResponses.All(b => b))
                    {
                        StopTrial();
                    }
                    else
                    {
                        // If there are still more products to select, hide this product
                        product.SetProductVisibility(false);
                    }
                }
                // Otherwise, the wrong product was selected
                else
                {
                    // Play a sound effect here too
                    SoundEffectPlayer.Instance.PlayIncorrectProductSelected();
                }
            }
        }

        public void ResetHiddenProducts()
        {
            foreach (Transform child in InLayoutProducts.transform)
            {
                if (!child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                }
            }
            foreach (Transform child in OutLayoutProducts.transform)
            {
                if (!child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        public void ResponseGiven(string response)
        {
            if (!TrialActive)
                return;

            // Repurpose the list of selected objects to put our response in
            currentTrialDataLog.SelectedObjectNames.Add(response);

            // Play a sound effect
            SoundEffectPlayer.Instance.PlayHypothesisResponseGiven();

            // The trial finishes immediately when a response is given
            StopTrial();
        }

        #endregion Participant interaction methods

        #region Data logging functions

        private void InitialiseDataLogging()
        {
            if (!LoggingEnabled)
                return;

            if (!FolderPath.EndsWith('/'))
                FolderPath += '/';

            // Create stream writer for the main set of aggregated data
            // Get the path, which differs if we use a new file per each participant
            string path = NewFilePerParticipant ? string.Format("{0}MainData_Participant{1}.csv", FolderPath, CurrentParticipantID) : string.Format("{0}MainData.csv", FolderPath);
            // If the file doesn't already exist, we need to write headers to it
            bool writeHeaders = !File.Exists(path);
            // Create the stream writer
            mainDataStreamWriter = new StreamWriter(path, true);
            // Append headers if needed
            if (writeHeaders)
                mainDataStreamWriter.WriteLine("Participant ID,Technique,FOV,Task Type, Question ID,Completion Time, Time until last selection on tablet,Time until selected first object,Count of selected Objects,Count of Wrong Selected Objects,Name of selected Objects");
            mainDataStreamWriter.AutoFlush = true;

            // Create stream writer for the interactions
            if (LogInteractions)
            {
                path = string.Format("{0}InteractionsData_Participant{1}.csv", FolderPath, CurrentParticipantID);
                writeHeaders = !File.Exists(path);
                interactionsDataStreamWriter = new StreamWriter(path, true);
                if (writeHeaders)
                    interactionsDataStreamWriter.WriteLine("Technique,FOV,Task Type,Question ID,Time,Interaction Type,Comment");
                interactionsDataStreamWriter.AutoFlush = true;
            }
        }

        private void WriteDataLogging()
        {
            if (!LoggingEnabled || mainDataStreamWriter == null || CurrentTrial.Task == TaskType.Tutorial)
                return;

            mainDataStreamWriter.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                                                            CurrentParticipantID,
                                                            CurrentTrial.Technique.ToString(),
                                                            CurrentTrial.Layout.ToString(),
                                                            CurrentTrial.Task.ToString(),
                                                            CurrentTrial.QuestionID,
                                                            currentTrialDataLog.CompletionTime,
                                                            currentTrialDataLog.TimeUntilLastTabletInteraction,
                                                            currentTrialDataLog.TimeUntilFirstObjectSelected,
                                                            currentTrialDataLog.CountOfSelectedObjects,
                                                            currentTrialDataLog.CountOfWrongSelectedObjects,
                                                            string.Join(';', currentTrialDataLog.SelectedObjectNames)
            ));
        }

        private void LogInteractionData(float time, InteractionType interactionType, string comment = "")
        {
            if (!LoggingEnabled || interactionsDataStreamWriter == null || CurrentTrial.Task == TaskType.Tutorial)
                return;

            interactionsDataStreamWriter.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6}",
                                                                 CurrentTrial.Technique.ToString(),
                                                                 CurrentTrial.Layout.ToString(),
                                                                 CurrentTrial.Task.ToString(),
                                                                 CurrentTrial.QuestionID,
                                                                 time,
                                                                 interactionType.ToString(),
                                                                 comment
                                                        ));
        }

        private void StopDataLogging()
        {
            if (LoggingEnabled && mainDataStreamWriter != null)
            {
                mainDataStreamWriter.Close();
                interactionsDataStreamWriter.Close();
            }
        }

        private void OnApplicationQuit()
        {
            StopDataLogging();
        }

        #endregion Data logging functions
    }


    public enum HighlightTechnique
    {
        None,
        Color,
        Outline,
        Link,
        Arrow,
        Size
    }

    public enum ShelfLayout
    {
        None,
        Tutorial,
        In,
        Out,
        Training
    }

    public enum TaskType
    {
        Tutorial,
        Single,
        Multiple,
        Hypothesis
    }

    public enum InteractionType
    {
        Brushing,
        Filter,
        DimensionChange,
        BrushChange,
        ProductSelect
    }

    public class StudyTask
    {
        public string QuestionID;
        public string QuestionText;
        public string[] QuestionAnswers;
        public string DimensionName1;
        public string DimensionDirection1;
        public string DimensionThreshold1;
        public string DimensionName2;
        public string DimensionDirection2;
        public string DimensionThreshold2;

        public StudyTask(string questionID, string questionText, string[] questionAnswers, string dimensionName1, string dimensionDirection1, string dimensionThreshold1, string dimensionName2, string dimensionDirection2, string dimensionThreshold2)
        {
            this.QuestionID = questionID;
            this.QuestionText = questionText;
            this.QuestionAnswers = questionAnswers;
            this.DimensionName1 = dimensionName1;
            this.DimensionDirection1 = dimensionDirection1;
            this.DimensionThreshold1 = dimensionThreshold1;
            this.DimensionName2 = dimensionName2;
            this.DimensionDirection2 = dimensionDirection2;
            this.DimensionThreshold2 = dimensionThreshold2;
        }
    }

    public class StudyTrial
    {
        public int TrialNumber;
        public HighlightTechnique Technique;
        public ShelfLayout Layout;
        public TaskType Task;
        public string QuestionID;
        public string QuestionText;
        public string[] QuestionAnswers;
        public bool[] QuestionResponses;
        public string DimensionName1;
        public string DimensionDirection1;
        public string DimensionThreshold1;
        public string DimensionName2;
        public string DimensionDirection2;
        public string DimensionThreshold2;
        public bool IsTrialCompleted = false;

        public StudyTrial(HighlightTechnique technique, ShelfLayout layout, TaskType task)
        {
            this.Technique = technique;
            this.Layout = layout;
            this.Task = task;
        }

        public StudyTrial(HighlightTechnique technique, ShelfLayout layout, TaskType task, StudyTask studyTask)
        {
            this.Technique = technique;
            this.Layout = layout;
            this.Task = task;
            this.QuestionID = studyTask.QuestionID;
            this.QuestionText = studyTask.QuestionText;
            this.QuestionAnswers = studyTask.QuestionAnswers;
            this.QuestionResponses = new bool[studyTask.QuestionAnswers.Length];
            this.DimensionName1 = studyTask.DimensionName1;
            this.DimensionDirection1 = studyTask.DimensionDirection1;
            this.DimensionThreshold1 = studyTask.DimensionThreshold1;
            this.DimensionName2 = studyTask.DimensionName2;
            this.DimensionDirection2 = studyTask.DimensionDirection2;
            this.DimensionThreshold2 = studyTask.DimensionThreshold2;
        }


        public StudyTrial(HighlightTechnique technique, ShelfLayout layout, TaskType task, string questionID, string questionText, string[] questionAnswers, string dimensionName1, string dimensionDirection1, string dimensionThreshold1, string dimensionName2, string dimensionDirection2, string dimensionThreshold2)
        {
            this.Technique = technique;
            this.Layout = layout;
            this.Task = task;
            this.QuestionID = questionID;
            this.QuestionText = questionText;
            this.QuestionAnswers = questionAnswers;
            this.QuestionResponses = new bool[questionAnswers.Length];
            this.DimensionName1 = dimensionName1;
            this.DimensionDirection1 = dimensionDirection1;
            this.DimensionThreshold1 = dimensionThreshold1;
            this.DimensionName2 = dimensionName2;
            this.DimensionDirection2 = dimensionDirection2;
            this.DimensionThreshold2 = dimensionThreshold2;
        }

        public void TrialCompleted()
        {
            IsTrialCompleted = true;
        }

        public string ToFormattedString()
        {
            if (Task == TaskType.Tutorial)
            {
                return string.Format("Trial Number: {0}\nTechnique: {1}\nLayout: {2}\nTask Type: {3}",
                            TrialNumber,
                            Technique.ToString(),
                            Layout.ToString(),
                            Task.ToString());
            }
            else
            {
                return string.Format("Trial Number: {0}\nTechnique: {1}\nLayout: {2}\nTask Type: {3}\nTask ID: {4}\nTask Question: {5}\nTask Answers: {6}",
                                            TrialNumber,
                                            Technique.ToString(),
                                            Layout.ToString(),
                                            Task.ToString(),
                                            QuestionID,
                                            QuestionText,
                                            string.Join(", ", QuestionAnswers)
                                            );
            }
        }
    }

    public class TrialDataLog
    {
        public float CompletionTime;
        public float TimeUntilLastTabletInteraction;
        public float TimeUntilFirstObjectSelected;
        public int CountOfSelectedObjects;
        public int CountOfWrongSelectedObjects;
        public List<string> SelectedObjectNames = new List<string>();
    }
}
