import { Moon, Sun, Monitor } from 'lucide-react';
import { useTheme } from 'next-themes';
import { SidebarMenuButton } from '@/components/ui/sidebar';

export function ModeToggle() {
  const { theme, setTheme } = useTheme();

  const cycle = () => {
    if (theme === 'light') setTheme('dark');
    else if (theme === 'dark') setTheme('system');
    else setTheme('light');
  };

  const icon =
    theme === 'dark' ? <Moon /> : theme === 'light' ? <Sun /> : <Monitor />;

  const label =
    theme === 'dark' ? 'Dark' : theme === 'light' ? 'Light' : 'System';

  return (
    <SidebarMenuButton
      tooltip={{ children: `Theme: ${label}`, hidden: false }}
      onClick={cycle}
      className="px-2.5 md:px-2"
    >
      {icon}
      <span>{label}</span>
    </SidebarMenuButton>
  );
}
