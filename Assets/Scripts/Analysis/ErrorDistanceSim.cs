using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace BrushingAndLinking
{
    public class ErrorDistanceSim : MonoBehaviour
    {
        public TextAsset StudyResults;
        public TextAsset TaskInfo;
        public string FilePath;

        private List<string[]> studyResultData;
        private Dictionary<string, string[]> taskAnswers;
        private List<string[]> processedData;

        private StreamWriter errorDistanceWriter;

        private void Start()
        {
            if (StudyResults == null)
                return;

            Debug.LogWarning("ErrorDistanceSim started");

            ParseTaskAnswers();
            ParseData();
            ProcessAndSimulateData();
            WriteToCsv();

            Debug.LogWarning("ErrorDistanceSim finished");
        }

        private void ParseTaskAnswers()
        {
            taskAnswers = new Dictionary<string, string[]>();

            string[] tasksSplit = TaskInfo.text.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries);

            // Read line by line, skipping header and statement questions
            for (int i = 1; i < tasksSplit.Length; i++)
            {
                string[] lineSplit = tasksSplit[i].Split(',');

                if (lineSplit[0] == "Hypothesis")
                    continue;

                string id = lineSplit[2];
                string[] answers = lineSplit[4].Split(';');
                taskAnswers.Add(id, answers);
            }
        }

        private void ParseData()
        {
            studyResultData = new List<string[]>();

            // Parse results csv
            string[] resultsSplit = StudyResults.text.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries);

            // Read line by line, skipping header
            for (int i = 1; i < resultsSplit.Length; i++)
            {
                string[] lineSplit = resultsSplit[i].Split(',');
                studyResultData.Add(lineSplit);
            }
        }

        /// <summary>
        /// Creates a list of arrays with the following logical columns:
        /// Participant ID, Technique, FOV, Task Type, Question, Euclidean Distance, Angular Distance from Center (origin)
        /// </summary>
        private void ProcessAndSimulateData()
        {
            processedData = new List<string[]>();

            foreach (string[] line in studyResultData)
            {
                // If this trial had no incorrectly selected products, or if it is a statement question, we skip
                if (float.Parse(line[9]) == 0 || line[3] == "Hypothesis")
                    continue;

                // Loop through each selected object, skipping those that are actually correct
                string[] selectedObjects = line[10].Split(';', System.StringSplitOptions.RemoveEmptyEntries);
                string[] answers = taskAnswers[line[4]];
                foreach (string selectedObject in selectedObjects)
                {
                    // If the array of answers contains the selected objects, then it is correct
                    if (answers.Contains(selectedObject))
                        continue;

                    // Get the closest correct object to the selected object
                    GameObject correctGameObject, selectedGameObject;
                    GetCorrectAndSelectedGameObjects(answers, selectedObject, out correctGameObject, out selectedGameObject);

                    // Copy over metadata of the trial
                    string[] trialData = new string[9];
                    trialData[0] = line[0];
                    trialData[1] = line[1];
                    trialData[2] = line[2];
                    trialData[3] = line[3];
                    trialData[4] = line[4];

                    // Add the rest of the data
                    trialData[5] = correctGameObject.name;
                    trialData[6] = selectedObject;
                    trialData[7] = Vector3.Distance(correctGameObject.transform.position, selectedGameObject.transform.position).ToString();
                    trialData[8] = Vector3.Angle(correctGameObject.transform.position.normalized, selectedGameObject.transform.position.normalized).ToString();      // Assume the user is at the origin (0, 0, 0)

                    Debug.LogWarning(string.Join(" ", trialData));
                    processedData.Add(trialData);
                }
            }
        }

        private void GetCorrectAndSelectedGameObjects(string[] answers, string selected, out GameObject correctGameObject, out GameObject selectedGameObject)
        {
            GameObject selectedGO = GameObject.Find(selected);

            correctGameObject = answers.Select(name => GameObject.Find(name))
                .OrderBy(go => Vector3.Distance(go.transform.position, selectedGO.transform.position))
                .First();

            selectedGameObject = selectedGO;
        }

        private void WriteToCsv()
        {
            if (!FilePath.EndsWith('/'))
                FilePath += '/';
            errorDistanceWriter = new StreamWriter(FilePath + "ErrorDistancesAndAngles.csv", true);

            errorDistanceWriter.WriteLine("ParticipantID,Technique,FOV,TaskType,Question,ClosestCorrectObject,SelectedObject,EuclideanDistance,AngularDistance");

            foreach (string[] line in processedData)
            {
                errorDistanceWriter.WriteLine(string.Join(',', line));
            }

            errorDistanceWriter.Close();
        }
    }
}