/**
 * 登录页 —— 独立账号 + Bearer token 会话（认证权限契约 v0.2）。
 * 错误：默认展示后端 message（LOGIN_FAILED/USER_DISABLED → 401 语义）。
 */
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, Package } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import {
  Card, CardContent, CardDescription, CardHeader, CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/platform/auth/auth-context'

interface LoginForm {
  username: string
  password: string
}

export function LoginPage() {
  const { t } = useTranslation()
  const { login, status } = useAuth()
  const navigate = useNavigate()
  const [submitting, setSubmitting] = useState(false)

  const schema = z.object({
    username: z.string().min(1, t('auth.usernameRequired')),
    password: z.string().min(1, t('auth.passwordRequired')),
  })

  const { register, handleSubmit, formState: { errors } } = useForm<LoginForm>({
    resolver: zodResolver(schema),
    defaultValues: { username: '', password: '' },
  })

  if (status === 'authed') return <Navigate to="/web" replace />

  const onSubmit = handleSubmit(async (values) => {
    setSubmitting(true)
    try {
      await login(values.username, values.password)
      navigate('/web', { replace: true })
    } catch (e) {
      toast.error((e as Error).message || t('auth.loginFailed'))
    } finally {
      setSubmitting(false)
    }
  })

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="items-center text-center">
          <span className="mb-2 flex size-11 items-center justify-center rounded-xl bg-primary text-primary-foreground">
            <Package className="size-5" data-icon />
          </span>
          <CardTitle className="text-lg">{t('auth.loginTitle')}</CardTitle>
          <CardDescription>{t('common.appName')}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="space-y-4" data-testid="login-form">
            <div className="space-y-1.5">
              <Label htmlFor="username">{t('auth.username')}</Label>
              <Input
                id="username"
                autoComplete="username"
                placeholder={t('auth.username')}
                aria-invalid={!!errors.username}
                {...register('username')}
              />
              {errors.username && <p className="text-xs text-destructive">{errors.username.message}</p>}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="password">{t('auth.password')}</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                placeholder={t('auth.password')}
                aria-invalid={!!errors.password}
                {...register('password')}
              />
              {errors.password && <p className="text-xs text-destructive">{errors.password.message}</p>}
            </div>
            <Button type="submit" className="w-full" disabled={submitting} data-testid="login-submit">
              {submitting && <Loader2 className="size-4 animate-spin" data-icon />}
              {t('auth.submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
