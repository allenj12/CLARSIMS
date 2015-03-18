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

        static int num_agents = 5;
        static int numTestTrials = num_agents + 2;

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
        static Agent[] actors = new Agent[num_agents];
        static double[][] affects = new double[num_agents][];

        static double[][] drives = new double[num_agents][];

        // affect look up table
        static double[, ,] affect_table = new double[6, 3, 3];

        static Random rand = new Random();

        private static TextWriter orig = Console.Out;
        private static StreamWriter sw = File.CreateText("results.txt");

        public static void Main()
        {
            Console.WriteLine("Initializing the Task");

            InitializeWorld();

            //the confederate "agent" will be easier implented as just a set of the drives it wants emulate
            //since the experiment the confederate was just an actor and not a test subject
            double[] confederate = new double[6];

            //create the rest of the agents and store them
            for (int i = 0; i < num_agents; i++)
            {
                Agent actor = World.NewAgent(i.ToString());
                InitializeAgent(actor);
            }

            // will start the simulation
            // will give the material out and compute all the pre non confederate affects
            // each agent will give a "presentation" then re compute the affect
            // compute final affect and benchmarks
            Test();

            for (int i = 0; i < num_agents; i++)
            {
                Agent actor = actors[i];
                actor.Die();
            }

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

            affect_table[AandB, low, low] = .05;
            affect_table[AandB, low, med] = .1;
            affect_table[AandB, low, high] = 1.5;

            affect_table[AandB, med, low] = -.05;
            affect_table[AandB, med, med] = .05;
            affect_table[AandB, med, high] = .3;

            affect_table[AandB, high, low] = -.1;
            affect_table[AandB, high, med] = .3;
            affect_table[AandB, high, high] = .7;

            //--

            affect_table[auto, low, low] = .05;
            affect_table[auto, low, med] = .1;
            affect_table[auto, low, high] = 1.5;

            affect_table[auto, med, low] = -.1;
            affect_table[auto, med, med] = .3;
            affect_table[auto, med, high] = .5;

            affect_table[auto, high, low] = -.3;
            affect_table[auto, high, med] = .3;
            affect_table[auto, high, high] = .7;

            //-- simulance seems it would be similar to AandB

            affect_table[sim, low, low] = .05;
            affect_table[sim, low, med] = .1;
            affect_table[sim, low, high] = 1.5;

            affect_table[sim, med, low] = -.05;
            affect_table[sim, med, med] = .05;
            affect_table[sim, med, high] = .3;

            affect_table[sim, high, low] = -.1;
            affect_table[sim, high, med] = .3;
            affect_table[sim, high, high] = .7;

            //-- similar to autonomy but slightly higher

            affect_table[fair, low, low] = .05;
            affect_table[fair, low, med] = .1;
            affect_table[fair, low, high] = 1.5;

            affect_table[fair, med, low] = -.2;
            affect_table[fair, med, med] = .4;
            affect_table[fair, med, high] = .6;

            affect_table[fair, high, low] = -.5;
            affect_table[fair, high, med] = .4;
            affect_table[fair, high, high] = .8;

            //--

            affect_table[DandP, low, low] = .05;
            affect_table[DandP, low, med] = .1;
            affect_table[DandP, low, high] = 1.5;

            affect_table[DandP, med, low] = -.3;
            affect_table[DandP, med, med] = .4;
            affect_table[DandP, med, high] = .5;

            affect_table[DandP, high, low] = -.8;
            affect_table[DandP, high, med] = -.1;
            affect_table[DandP, high, high] = .8;

            //--

            affect_table[hon, low, low] = .05;
            affect_table[hon, low, med] = .1;
            affect_table[hon, low, high] = 1.5;

            affect_table[hon, med, low] = -.3;
            affect_table[hon, med, med] = .4;
            affect_table[hon, med, high] = .5;

            affect_table[hon, high, low] = -.8;
            affect_table[hon, high, med] = -.1;
            affect_table[hon, high, high] = .8;




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
            autonomy.Commit(abEq);
            actor.Commit(autonomy);

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
            Console.Write("Performing Task...");

            //generate confederates mood
            int index = rand.Next(moods.Length);
            string mood = moods[index];

            double confederate_affect = 0;

            if (mood == "cheerful")
            {
                confederate_affect = .9;
            }

            else if (mood == "serene")
            {
                confederate_affect = .3;
            }

            else if (mood == "hostile")
            {
                confederate_affect = -.9;
            }

            else if (mood == "depressed")
            {
                confederate_affect = -.3;
            }

            Console.WriteLine(mood);

            //each agent reads the materials to be discussed and percieves them differently depending 
            //on there department/candidate or how they percieve the group relations 
            //which right now is random
            for (int i = 0; i < num_agents; i++)
            {
                Agent actor = actors[i];
                SensoryInformation si = World.NewSensoryInformation(actor);

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


                actor.Perceive(si);

                //compute initial affect for all the agents
                double initAffect = computeInitialAffect(i, drives[i]);
                affects[i][0] = initAffect;
            }

            //each agent gives its presentation to the others and we record the affects
            for (int i = 1; i < numTestTrials; i++)
            {
                for (int j = -1; i < num_agents; j++)
                {
                    if (j == -1)
                    {
                        double presnterAffect = confederate_affect;
                    }
                    else
                    {
                        double presenterAffect = affects[j][i - 1];
                    }
                    //curAffect will be the look up table after some details are worked out
                    //avgAffect(agent_name) will be time waited and not a true average

                    for (int k = 0; k < num_agents; k++)
                    {
                        if (k != j)
                        {
                            //own affect                    //other's time weighted affect
                            affects[k][i] = alpha * affects[k][i - 1] + beta * lambda * averageAffect(affects[j]) + affects[j][i - 1] / (1 - lambda);
                        }
                    }
                }
            }

            Console.SetOut(sw);

            Console.WriteLine(mood);

            Console.Write("here is a list of the affects array (each agent is its own array in the affects array in order)");
            Console.WriteLine("------------------------------------");
            Console.WriteLine(affects);
            Console.WriteLine("------------------------------------");
            Console.SetOut(orig);
            Console.WriteLine("Finished");
        }
        public static void PreTrainingEquation(ActivationCollection input, ActivationCollection output)
        //layed out all possible scenarios and just commented out the scenarios that shouldnt happen
        {

        }

        public static double computeInitialAffect(int agen, double[] drvs)
        {
            int ind = 0;
            double max = 0;
            double min = 1;

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

                max = Math.Max(max, affect_table[agen, ind, high]);
                min = Math.Min(min, affect_table[agen, ind, high]);
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