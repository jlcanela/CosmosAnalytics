import '@mantine/core/styles.css';
import { MantineProvider } from '@mantine/core';
import { Layout } from './components/Layout';

function App() {
  return (
    <MantineProvider>
      <Layout>
        <h1>Welcome to the Home Page!</h1>
      <p>This content overrides AppShell.Main.</p>
      </Layout>
    </MantineProvider>
  );
}

export default App;
