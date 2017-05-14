using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public static class UnifrogCommand
	{
		[Command(names: new string[] { "unifrog", "dat boi" }, desc: "o shit", usage: "unifrog", hidden: true)]
		public static async Task<string> Unifrog(DiscordBot bot, IMessage msg, string args)
		{
			var frame = 0;
			var count = 0;
			var newMsg = await (msg.Channel as IMessageChannel).SendMessageAsync("```" + _frames[frame] + "```");

			var timer = new Timer();
			timer.Elapsed += async (s, e) => {
				count++;

				frame++;
				if(frame >= _frames.Length)
				{
					frame = 0;
				}
				await newMsg.ModifyAsync((m) => m.Content = "```" + _frames[frame] + "```");
				if(count > 5)
				{
					timer.Enabled = false;
					await newMsg.DeleteAsync();
				}
			};

			timer.Interval = 500;
			timer.Enabled = true;
			return "";
		}

		[Command(names: new string[] { "aaron" }, hidden: true)]
		public static async Task<string> Aaron(DiscordBot bot, IMessage msg, string args)
		{
			var frame = 0;
			var count = 0;
			var newMsg = await (msg.Channel as IMessageChannel).SendMessageAsync("```" + _aaronFrames[frame] + "```");

			var timer = new Timer();
			timer.Elapsed += async (s, e) => {
				count++;

				frame++;
				if(frame >= _aaronFrames.Length)
				{
					frame = 0;
				}
				await newMsg.ModifyAsync((m) => m.Content = "```" + _aaronFrames[frame] + "```");

				if(count > 10)
				{
					timer.Enabled = false;
				}
			};

			timer.Interval = 1000;
			timer.Enabled = true;
			return "";
		}

		private static string[] _frames = 
		{
@"
             >:         
           ;--;-        
            .;---       
            ..;;:       
             --::       
            -;--:-      
          :-.-;-;:      
       >:;   ;:---:     
     ..      ;---.      
             ;;;;-      
             -;;:;:     
            -;.;..:!    
            ;.-> .:-    
           ;. !.! --    
           ;      7;    
           ;;  C   -    
            >;    :-    
             -;! --;    
            - - > :     
           -     ;-     
           ->>-?--;     
           C   : - -    
          >  --->:->    
          >.;!--.;>>    
           - -.;  .     
           ->>--- -     
           ------->     
            ..-- -      
             -..!       
",
@"
             ?:?        
           ;-:-.        
            ..:--       
            !.;;:       
             ;--:!      
            !;--:-      
         ?:-.;--;:      
       --;   ;---:;     
      -      ;---;:     
             ;;;;-!     
             -;;;;:     
            7-.;..-:    
            -;. ;; -:   
           >.:-->   --  
           ;        >;  
           :        :;  
           !   7    -.  
           :>- - -  ;   
            ;->   > ;   
           -.     . ;   
           -; -?-..     
          -?:->!  --    
           .--:--  ;    
          >.-;;; !>:    
           > --;> .;-   
           ;:  -- -     
           --->  ->     
            - - -       
             --.!       
",
@"
             >:         
           --: -        
           ..;:-        
            ..;--       
             ;--:       
            ;;--:-      
         :--:;--;:-     
      >-;.   -----      
             ---;-      
             ;;;;-      
             ;;;;;!7    
            7-;..-;::   
            -;;;..  ;:  
            -;---    ;  
            -. 7    ;;  
            -  !    ;;  
            :       ;   
            :--- >  ;   
            -->>        
           -   --.;;    
           -:> -  ---!  
           C- ->>: -    
            ! -;>>..    
           -- -;.  >    
           >-  ->       
           -;---->-     
           -  -----     
            -- --.      
             ;.--       
",
@"
             ::         
          >::-.!        
           ..;--        
            ..;:!       
            ;;-:-       
            ;---:       
          -- -;-;-      
       :-;   ----:7     
    !.;      -----      
             ;;;;:      
             ;;;-;:7    
            --....-::   
             ;;-.; ;:-  
            >;---   :;  
             ; :   --   
             . >  7;    
            >.    -:    
             -.! -      
            - > > >     
           - ;!   -     
           - -- --      
           C -.!  >-    
           >-.!:   -    
            >;.- ->-    
           > --;;--     
           -> !>> -     
           . -:  --     
            --- -       
             -;.;       
",
@"
             ::         
          >-:- !        
           ..!--        
            ..;:!       
            ;;-:-       
            ;-;--       
          -- -----      
       ::.>  ---::-     
     -;      ----;      
             ;;;;:      
             ;;;-;-     
            -;;...!-    
            ;;.-.;::    
            ;;-;-  -    
            ;. >  :-    
            ;  : -:;    
            -:   :;     
             ;.:--      
            - .::.>     
           >. --.-.     
           ; > ;  .     
           C> ;:-- >    
           - --. >-.    
           -  > -> -    
           >-  -.>-     
           - ---!!-     
           -  ---.      
            -  -;-      
             --.        
"
		};

		private static string[] _aaronFrames =
		{
@"
 /$$$$$$$   /$$$$$$   /$$$$$$  /$$      /$$ /$$
| $$__  $$ /$$__  $$ /$$__  $$| $$$    /$$$| $$
| $$  \ $$| $$  \ $$| $$  \ $$| $$$$  /$$$$| $$
| $$$$$$$ | $$  | $$| $$  | $$| $$ $$/$$ $$| $$
| $$__  $$| $$  | $$| $$  | $$| $$  $$$| $$|__/
| $$  \ $$| $$  | $$| $$  | $$| $$\  $ | $$    
| $$$$$$$/|  $$$$$$/|  $$$$$$/| $$ \/  | $$ /$$
|_______/  \______/  \______/ |__/     |__/|__/
",
@"
BBBBBBBBBBBBBBBBB        OOOOOOOOO          OOOOOOOOO     MMMMMMMM               MMMMMMMM !!! 
B::::::::::::::::B     OO:::::::::OO      OO:::::::::OO   M:::::::M             M:::::::M!!:!!
B::::::BBBBBB:::::B  OO:::::::::::::OO  OO:::::::::::::OO M::::::::M           M::::::::M!:::!
BB:::::B     B:::::BO:::::::OOO:::::::OO:::::::OOO:::::::OM:::::::::M         M:::::::::M!:::!
  B::::B     B:::::BO::::::O   O::::::OO::::::O   O::::::OM::::::::::M       M::::::::::M!:::!
  B::::B     B:::::BO:::::O     O:::::OO:::::O     O:::::OM:::::::::::M     M:::::::::::M!:::!
  B::::BBBBBB:::::B O:::::O     O:::::OO:::::O     O:::::OM:::::::M::::M   M::::M:::::::M!:::!
  B:::::::::::::BB  O:::::O     O:::::OO:::::O     O:::::OM::::::M M::::M M::::M M::::::M!:::!
  B::::BBBBBB:::::B O:::::O     O:::::OO:::::O     O:::::OM::::::M  M::::M::::M  M::::::M!:::!
  B::::B     B:::::BO:::::O     O:::::OO:::::O     O:::::OM::::::M   M:::::::M   M::::::M!:::!
  B::::B     B:::::BO:::::O     O:::::OO:::::O     O:::::OM::::::M    M:::::M    M::::::M!!:!!
  B::::B     B:::::BO::::::O   O::::::OO::::::O   O::::::OM::::::M     MMMMM     M::::::M !!! 
BB:::::BBBBBB::::::BO:::::::OOO:::::::OO:::::::OOO:::::::OM::::::M               M::::::M     
B:::::::::::::::::B  OO:::::::::::::OO  OO:::::::::::::OO M::::::M               M::::::M !!! 
B::::::::::::::::B     OO:::::::::OO      OO:::::::::OO   M::::::M               M::::::M!!:!!
BBBBBBBBBBBBBBBBB        OOOOOOOOO          OOOOOOOOO     MMMMMMMM               MMMMMMMM !!! 
",
@"
______/\\\\\\\\\\\_____/\\\\\\\\\_____/\\\\____________/\\\\_____/\\\____        
 _____\/////\\\///____/\\\\\\\\\\\\\__\/\\\\\\________/\\\\\\___/\\\\\\\__       
  _________\/\\\______/\\\/////////\\\_\/\\\//\\\____/\\\//\\\__/\\\\\\\\\_      
   _________\/\\\_____\/\\\_______\/\\\_\/\\\\///\\\/\\\/_\/\\\_\//\\\\\\\__     
    _________\/\\\_____\/\\\\\\\\\\\\\\\_\/\\\__\///\\\/___\/\\\__\//\\\\\___    
     _________\/\\\_____\/\\\/////////\\\_\/\\\____\///_____\/\\\___\//\\\____   
      __/\\\___\/\\\_____\/\\\_______\/\\\_\/\\\_____________\/\\\____\///_____  
       _\//\\\\\\\\\______\/\\\_______\/\\\_\/\\\_____________\/\\\_____/\\\____ 
        __\/////////_______\///________\///__\///______________\///_____\///_____ 
",
@"
     ██╗ █████╗ ███╗   ███╗██╗
     ██║██╔══██╗████╗ ████║██║
     ██║███████║██╔████╔██║██║
██   ██║██╔══██║██║╚██╔╝██║╚═╝
╚█████╔╝██║  ██║██║ ╚═╝ ██║██╗
 ╚════╝ ╚═╝  ╚═╝╚═╝     ╚═╝╚═╝
"
		};
	}
}
