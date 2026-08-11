// RORSH-Gate ASCII Art Banner
// No unicode or emoji - pure ASCII

namespace RorshGate.Assets
{
    public static class AsciiArt
    {
        public static readonly string[] Banner = new string[]
        {
            "",
            " ######   #######   ######   #    #   #    #          ######      #     ########  ########  ",
            " #     #  #     #  #     #  #   #    #    #          #          # #       #     #         ",
            " #     #  #     #  #     #  #  #     #    #          #         #   #      #     #         ",
            " ######   #     #  ######   ###      ######   #####   #   ###  #######     #     #####     ",
            " #   #    #     #  #   #    #  #     #    #          #     #  #     #     #     #         ",
            " #    #   #     #  #    #   #   #    #    #          #     #  #     #     #     #         ",
            " #     #  #######   #     #  #    #   #    #           #####   #     #     #     ########  ",
            "",
            "                    Secure File Distribution CLI Tool",
            ""
        };

        public static void PrintBanner()
        {
            foreach (string line in Banner)
            {
                System.Console.WriteLine(line);
            }
        }
    }
}
