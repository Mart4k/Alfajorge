import { cp, mkdir, rm } from 'node:fs/promises'
import { spawn } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const output = resolve(root, 'dist')

function run(command, args, env = {}) {
  return new Promise((resolvePromise, reject) => {
    const process = spawn(command, args, {
      cwd: root,
      env: { ...globalThis.process.env, ...env },
      stdio: 'inherit',
      shell: true
    })
    process.on('error', reject)
    process.on('close', code => code === 0 ? resolvePromise() : reject(new Error(`${command} terminó con código ${code}`)))
  })
}

await rm(output, { recursive: true, force: true })
await mkdir(output, { recursive: true })

await run('npm', ['run', 'build', '--workspace', 'apps/clientes'], { VITE_APP_BASE: '/clientes/' })
await cp(resolve(root, 'apps/clientes/dist'), resolve(output, 'clientes'), { recursive: true })

await run('npm', ['run', 'build', '--workspace', 'apps/empleados'], { VITE_APP_BASE: '/empleados/' })
await cp(resolve(root, 'apps/empleados/dist'), resolve(output, 'empleados'), { recursive: true })

await cp(resolve(root, 'apps/shell/index.html'), resolve(output, 'index.html'))
