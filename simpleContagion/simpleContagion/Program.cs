using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Linq;
using Clarion;
using Clarion.Framework;
using Clarion.Framework.Templates;
using Clarion.Framework.Extensions;
using Clarion.Framework.Extensions.Templates;

namespace SimpleContagion
{
    public class Sim
    {
        public enum Groups { PRIVATE, PUBLIC }

        static int numAgents = 5;
        static int numTestTrials = numAgents + 2;

        // for affect equation
        static double lambda = 0.1;
        static double alpha = 0.5;
        static double beta = 1 - alpha;

        //will make reading  the affect table later easier.
        static int low = 0;
        static int med = 1;
        static int high = 2;

        static int AandB = 0;
        static int auto = 1;
        static int sim = 2;
        static int fair = 3;
        static int DandP = 4;
        static int hon = 5;

        //confederates mood
        static string[] moods = new string[4] { "cheerful", "serene", "hostile", "depressed" };


        //sets an array of agents that will be used in the simulation
        static Agent[] actors = new Agent[numAgents];
        static double[][] affects = new double[numAgents][];

        static double[][] drives = new double[numAgents][];

        // affect look up table
        static double[, ,] affectTable = new double[6, 3, 3];

        static Random rand = new Random();

        private static TextWriter orig = Console.Out;
        private static StreamWriter sw = File.CreateText("results.txt");

        public static void Main()
        {
            Console.WriteLine("Initializing the Task");

            InitializeWorld();

            //create the rest of the agents and store them
            for (int i = 0; i < numAgents; i++)
            {
                actors[i] = World.NewAgent(i.ToString());
                InitializeAgent(actors[i]);
            }

            // will start the simulation
            // will give the material out and compute all the pre non confederate affects
            // each agent will give a "presentation" then re compute the affect
            // compute final affect and benchmarks
            Test();

            /*
            for (int i = 0; i < numAgents; i++)
            {
                actors[i].Die();
            }
            */

            Console.WriteLine("Done");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        public static void InitializeWorld()
        {
            World.LoggingLevel = System.Diagnostics.TraceLevel.Off;
            //normally DV values in the world would normally be here but is not really needed until we expand the simulatiosn
            //for now we will have just the affect table in here with an estimate of what the affect should be (will later be ranges)
            //instead of flat doubles
            //indexing is by [drive name, drive value, action potential]

            affectTable[AandB, low, low] = .05;
            affectTable[AandB, low, med] = .1;
            affectTable[AandB, low, high] = 1.5;

            affectTable[AandB, med, low] = -.05;
            affectTable[AandB, med, med] = .05;
            affectTable[AandB, med, high] = .3;

            affectTable[AandB, high, low] = -.1;
            affectTable[AandB, high, med] = .3;
            affectTable[AandB, high, high] = .7;

            //--

            affectTable[auto, low, low] = .05;
            affectTable[auto, low, med] = .1;
            affectTable[auto, low, high] = 1.5;

            affectTable[auto, med, low] = -.1;
            affectTable[auto, med, med] = .3;
            affectTable[auto, med, high] = .5;

            affectTable[auto, high, low] = -.3;
            affectTable[auto, high, med] = .3;
            affectTable[auto, high, high] = .7;

            //-- simulance seems it would be similar to AandB

            affectTable[sim, low, low] = .05;
            affectTable[sim, low, med] = .1;
            affectTable[sim, low, high] = 1.5;

            affectTable[sim, med, low] = -.05;
            affectTable[sim, med, med] = .05;
            affectTable[sim, med, high] = .3;

            affectTable[sim, high, low] = -.1;
            affectTable[sim, high, med] = .3;
            affectTable[sim, high, high] = .7;

            //-- similar to autonomy but slightly higher

            affectTable[fair, low, low] = .05;
            affectTable[fair, low, med] = .1;
            affectTable[fair, low, high] = 1.5;

            affectTable[fair, med, low] = -.2;
            affectTable[fair, med, med] = .4;
            affectTable[fair, med, high] = .6;

            affectTable[fair, high, low] = -.5;
            affectTable[fair, high, med] = .4;
            affectTable[fair, high, high] = .8;

            //--

            affectTable[DandP, low, low] = .05;
            affectTable[DandP, low, med] = .1;
            affectTable[DandP, low, high] = 1.5;

            affectTable[DandP, med, low] = -.3;
            affectTable[DandP, med, med] = .4;
            affectTable[DandP, med, high] = .5;

            affectTable[DandP, high, low] = -.8;
            affectTable[DandP, high, med] = -.1;
            affectTable[DandP, high, high] = .8;

            //--

            affectTable[hon, low, low] = .05;
            affectTable[hon, low, med] = .1;
            affectTable[hon, low, high] = 1.5;

            affectTable[hon, med, low] = -.3;
            affectTable[hon, med, med] = .4;
            affectTable[hon, med, high] = .5;

            affectTable[hon, high, low] = -.8;
            affectTable[hon, high, med] = -.1;
            affectTable[hon, high, high] = .8;


            for (int i = 0; i < numAgents; i++)
            {
                drives[i] = new double[6];
                affects[i] = new double[numTestTrials];
            }

        }

        public static void InitializeAgent(Agent actor)
        {
            BPNetwork net = AgentInitializer.InitializeImplicitDecisionNetwork(actor, BPNetwork.Factory);

            actor.ACS.Parameters.PERFORM_RER_REFINEMENT = false;
            actor.ACS.Parameters.PERFORM_DELETION_BY_DENSITY = false;
            actor.ACS.Parameters.FIXED_BL_LEVEL_SELECTION_MEASURE = 1;
            actor.ACS.Parameters.FIXED_RER_LEVEL_SELECTION_MEASURE = 1;
            actor.ACS.Parameters.FIXED_IRL_LEVEL_SELECTION_MEASURE = 0;
            actor.ACS.Parameters.FIXED_FR_LEVEL_SELECTION_MEASURE = 0;

            // MS : drives that are relevant to this simulation
            // might change the random initialization to 0 or randomly small
            AffiliationBelongingnessDrive ab = AgentInitializer.InitializeDrive(actor, AffiliationBelongingnessDrive.Factory, rand.NextDouble());
            AutonomyDrive autonomy = AgentInitializer.InitializeDrive(actor, AutonomyDrive.Factory, rand.NextDouble());
            SimilanceDrive simulance = AgentInitializer.InitializeDrive(actor, SimilanceDrive.Factory, rand.NextDouble());
            DominancePowerDrive dominance = AgentInitializer.InitializeDrive(actor, DominancePowerDrive.Factory, rand.NextDouble());
            FairnessDrive fairness = AgentInitializer.InitializeDrive(actor, FairnessDrive.Factory, rand.NextDouble());
            HonorDrive honor = AgentInitializer.InitializeDrive(actor, HonorDrive.Factory, rand.NextDouble());

            //Affliation and Belongingness
            DriveEquation abEq = AgentInitializer.InitializeDriveComponent(ab, DriveEquation.Factory);
            ab.Commit(abEq);
            actor.Commit(ab);

            //autonomy
            DriveEquation autonomyEq = AgentInitializer.InitializeDriveComponent(autonomy, DriveEquation.Factory);
            autonomy.Commit(autonomyEq);
            actor.Commit(autonomy);

            //simulance
            DriveEquation simulanceEq = AgentInitializer.InitializeDriveComponent(simulance, DriveEquation.Factory);
            simulance.Commit(simulanceEq);
            actor.Commit(simulance);

            //dominance and power
            DriveEquation dominanceEq = AgentInitializer.InitializeDriveComponent(dominance, DriveEquation.Factory);
            dominance.Commit(dominanceEq);
            actor.Commit(dominance);

            //fairness
            DriveEquation fairnessEq = AgentInitializer.InitializeDriveComponent(fairness, DriveEquation.Factory);
            fairness.Commit(fairnessEq);
            actor.Commit(fairness);

            //honor
            DriveEquation honorEq = AgentInitializer.InitializeDriveComponent(honor, DriveEquation.Factory);
            honor.Commit(honorEq);
            actor.Commit(honor);

            //MCS
            // goals not needed now since there is no actions to generalize yet

        }

        //not needed until context is added
        public static void PreTrainACS(BPNetwork net)
        {

            //No ACS until model is more complicated
        }

        public static void Test()
        {
            Console.WriteLine("Performing Task...");

            //generate confederates mood
            int index = rand.Next(moods.Length);
            string mood = moods[index];

            double confederateAffect = 0;

            if (mood == "cheerful")
            {
                confederateAffect = .9;
            }

            else if (mood == "serene")
            {
                confederateAffect = .3;
            }

            else if (mood == "hostile")
            {
                confederateAffect = -.9;
            }

            else if (mood == "depressed")
            {
                confederateAffect = -.3;
            }

            Console.WriteLine("the Initial mood of the confederate is: {0}", mood);

            //each agent reads the materials to be discussed and percieves them differently depending 
            //on there department/candidate or how they percieve the group relations 
            //which right now is random
            for (int i = 0; i < numAgents; i++)
            {

                SensoryInformation si = World.NewSensoryInformation(actors[i]);

                double ab = rand.NextDouble();
                double aut = rand.NextDouble();
                double s = rand.NextDouble();
                double fai = rand.NextDouble();
                double dp = rand.NextDouble();
                double h = rand.NextDouble();

                si[Drive.MetaInfoReservations.STIMULUS, typeof(AffiliationBelongingnessDrive).Name] = ab;
                si[Drive.MetaInfoReservations.STIMULUS, typeof(AutonomyDrive).Name] = aut;
                si[Drive.MetaInfoReservations.STIMULUS, typeof(SimilanceDrive).Name] = s;
                si[Drive.MetaInfoReservations.STIMULUS, typeof(FairnessDrive).Name] = fai;
                si[Drive.MetaInfoReservations.STIMULUS, typeof(DominancePowerDrive).Name] = dp;
                si[Drive.MetaInfoReservations.STIMULUS, typeof(HonorDrive).Name] = h;

                //saves the drive values since there constant once the material is handed out for now
                //and is cleaner than using the Agent.Get methods
                drives[i][AandB] = ab;
                drives[i][auto] = aut;
                drives[i][sim] = s;
                drives[i][fair] = fai;
                drives[i][DandP] = dp;
                drives[i][hon] = h;


                actors[i].Perceive(si);

                //compute initial affect for all the agents
                double initAffect = computeInitialAffect(drives[i]);
                affects[i][0] = initAffect;
            }

            //each agent gives its presentation to the others and we record the affects
            double presenterAffect;
            double avgAffect;
            for (int i = 1; i < numTestTrials; i++)
            {
                for (int j = -1; j < numAgents; j++)
                {
                    if (j == -1)
                    {
                        presenterAffect = confederateAffect;
                        avgAffect = confederateAffect; 
                    }
                    else
                    {
                        presenterAffect = affects[j][i - 1];
                        avgAffect = averageAffect(affects[j]);
                    }
                    //curAffect will be the look up table after some details are worked out
                    //avgAffect(agent_name) will be time waited and not a true average

                    for (int k = 0; k < numAgents; k++)
                    {
                        if (k != j)
                        {
                                                //own affect                    //other's time weighted affect
                            affects[k][i] = alpha * affects[k][i - 1] + beta * (lambda * avgAffect + presenterAffect * (1 - lambda));
                        }
                    }
                }
            }

            //write a dataframe so we can analyze the data using R.
            Console.SetOut(sw);

            //header
            string[] header = new string[] { "confederate" ,"one", "two", "three", "four", "five" };

            for (int k = 0; k < numAgents + 1; k++)
            {
                Console.Write(header[k]);

                if (k != numAgents)
                {
                    Console.Write("\t");
                }
            }

            Console.Write("\n");

            //table
            for (int i = 0; i < numTestTrials; i++)
            {
                    Console.Write(confederateAffect);
                    Console.Write("\t");
 
                for (int k = 0; k < numAgents; k++)
                {
                        Console.Write(affects[k][i]);


                        if (k != numAgents - 1)
                        {
                            Console.Write("\t");
                        }
                }

                Console.Write("\n");
            }

            sw.Close();
            Console.SetOut(orig);
            Console.WriteLine("Finished");
        }
        public static void PreTrainingEquation(ActivationCollection input, ActivationCollection output)
        {

        }

        public static double computeInitialAffect(double[] drvs)
        {
            int ind = 0;
            double max = 0;
            double min = 0;

            for (int i = 0; i < drvs.Length; i++)
            {
                if (drvs[i] <= .3)
                {
                    ind = low;
                }

                else if (drvs[i] <= .7)
                {
                    ind = med;
                }

                else
                {
                    ind = high;
                }

                //for now randonm action potential
                int actionPotential = rand.Next(3);

                max = Math.Max(max, affectTable[i, ind, actionPotential]);
                min = Math.Min(min, affectTable[i, ind, actionPotential]);
            }
            return max + min;
        }

        public static double averageAffect(double[] affs)
        {
            double sum = 0;

            for (int i = 0; i < affs.Length - 1; i++)
            {
                sum += affs[i];
            }
            return sum / (affs.Length - 1);
        }
    }
}