import { Anchor, AppShell, Burger, Group, NavLink, Stack } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Link, useRouterState } from '@tanstack/react-router';
import React from 'react';

export function Layout({ children }: React.PropsWithChildren) {
  const [opened, { toggle }] = useDisclosure();
  const { location } = useRouterState();
  const isProjectManagement = location.pathname === '/';
  const isAbout = location.pathname === '/about';
  const isPath = (path: string) => location.pathname == path;

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{
        width: 300,
        breakpoint: 'sm',
        collapsed: { mobile: !opened },
      }}
      padding="md"
    >
      <AppShell.Header>
        <div style={{ display: 'flex', alignItems: 'center', height: '100%' }}>
          <Burger
            opened={opened}
            onClick={toggle}
            hiddenFrom="sm"
            size="sm"
            mr="md"
          />
          <div style={{ fontWeight: 700, marginRight: '2rem' }}>Logo</div>
          <Group gap="md">
            <Anchor
              component={Link}
              to="/"
              underline="hover"
              color={isProjectManagement ? 'blue' : 'gray'}
              fw={isProjectManagement ? 700 : 400}
              // Optionally add a style for a more visible indicator
              style={isProjectManagement ? { textDecoration: 'underline' } : undefined}
            >
              Project Management
            </Anchor>
            <Anchor
              component={Link}
              to="/about"
              underline="hover"
              color={isAbout ? 'blue' : 'gray'}
              fw={isAbout ? 700 : 400}
              // Optionally add a style for a more visible indicator
              style={isAbout ? { textDecoration: 'underline' } : undefined}
            >
              About
            </Anchor>            <Anchor
              href="/swagger"
              target="_blank"
              rel="noopener noreferrer"
              underline="hover"
            >
              API
            </Anchor>
          </Group>
        </div>
      </AppShell.Header>
      
      <AppShell.Navbar p="md">
        <Stack gap="xs">
          <NavLink
            label="Administration"
            component={Link}
            to="/admin"
            active={isPath("/admin")}
          />
           <NavLink
            label="Project Management"
            component={Link}
            to="/"
            active={isPath("/")}
          />
        </Stack>
      </AppShell.Navbar>

      <AppShell.Main>
        {children}
      </AppShell.Main>
    </AppShell>
  );
}
