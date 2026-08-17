// eslint-disable-next-line @typescript-eslint/ban-ts-comment -- TODO: retirar al completar el tipado heredado.
// @ts-nocheck
// TODO: tipar contratos heredados de API, props y estado antes de retirar esta supresión.
import { gsap } from 'gsap'
import { ScrollTrigger } from 'gsap/ScrollTrigger'

gsap.registerPlugin(ScrollTrigger)

const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)')
const revealSelector = '.w2-page-title, .w2-irrigation-strip, .w2-cards, .w2-tabs, .w2-inline-form, .w2-table-card, .w2-permissions, .w2-settings, .w2-timeline, .w2-login-form form, .iot-kpis, .iot-tabs, .iot-network, .iot-toolbar, .iot-grid, .iot-calibration, .m-head, .m-kpis, .m-grid, .m-panel, .m-quality, .m-farm, .m-rec-grid, .m-cycle-grid'

function addDepthScene(scope) {
  if (scope.querySelector(':scope > .w2-gsap-depth')) return
  const scene = document.createElement('div')
  scene.className = 'w2-gsap-depth'
  scene.setAttribute('aria-hidden', 'true')
  scene.innerHTML = '<i data-depth="0.65" data-parallax="24"></i><i data-depth="1.1" data-parallax="42"></i><i data-depth="1.45" data-parallax="58"></i>'
  scope.prepend(scene)
}

function animateNewContent(root = document) {
  if (reducedMotion.matches) return
  root.querySelectorAll(revealSelector).forEach((element) => {
    if (element.dataset.gsapReveal) return
    element.dataset.gsapReveal = 'true'
    gsap.from(element, {
      autoAlpha: 0,
      y: 24,
      duration: 0.72,
      ease: 'power3.out',
      clearProps: 'transform,opacity,visibility',
    })
  })

  root.querySelectorAll('.iot-kpis, .iot-grid, .iot-branches, .iot-sensor-ribbon, .m-kpis, .m-sensor-cards, .m-rec-grid, .m-cycle-grid, .m-zone-grid').forEach((group) => {
    if (group.dataset.gsapStagger) return
    group.dataset.gsapStagger = 'true'
    gsap.from(group.children, {
      autoAlpha: 0,
      y: 28,
      scale: 0.97,
      duration: 0.62,
      stagger: 0.09,
      ease: 'back.out(1.35)',
      clearProps: 'transform,opacity,visibility',
    })
  })

  root.querySelectorAll('.iot-network').forEach((network) => {
    if (network.dataset.gsapWaterScene) return
    network.dataset.gsapWaterScene = 'true'
    const hub = network.querySelector('.iot-hub')
    const branches = network.querySelector('.iot-branches')
    if (hub) gsap.to(hub, { y: -7, duration: 2.8, repeat: -1, yoyo: true, ease: 'sine.inOut' })
    if (branches) gsap.fromTo(branches, { y: 10 }, {
      y: -10,
      ease: 'none',
      scrollTrigger: { trigger: network, start: 'top bottom', end: 'bottom top', scrub: 1.1 },
    })
  })

  root.querySelectorAll('.iot-editor').forEach((editor) => {
    if (editor.dataset.gsapEditor) return
    editor.dataset.gsapEditor = 'true'
    gsap.from(editor.querySelectorAll('form > label'), {
      x: 22,
      autoAlpha: 0,
      duration: 0.38,
      stagger: 0.045,
      ease: 'power2.out',
      clearProps: 'transform,opacity,visibility',
    })
  })
}

function mountParallax(scope) {
  if (scope.dataset.gsapParallax || reducedMotion.matches) return
  scope.dataset.gsapParallax = 'true'
  addDepthScene(scope)
  const layers = Array.from(scope.querySelectorAll('.w2-gsap-depth [data-depth]'))
  const setters = layers.map((element) => ({
    depth: Number(element.dataset.depth),
    x: gsap.quickTo(element, 'x', { duration: 0.7, ease: 'power3.out' }),
  }))

  layers.forEach((element) => {
    const distance = Number(element.dataset.parallax)
    gsap.fromTo(element, { y: -distance }, {
      y: distance,
      ease: 'none',
      scrollTrigger: { trigger: scope, start: 'top bottom', end: 'bottom top', scrub: 0.8 },
    })
  })

  scope.addEventListener('pointermove', (event) => {
    const bounds = scope.getBoundingClientRect()
    const x = (event.clientX - bounds.left) / bounds.width - 0.5
    setters.forEach((layer) => {
      layer.x(x * 32 * layer.depth)
    })
  })
  scope.addEventListener('pointerleave', () => setters.forEach((layer) => layer.x(0)))
}

function refreshAnimations() {
  document.querySelectorAll('.w2-login-story, .w2-content, .m-water').forEach(mountParallax)
  animateNewContent()
  ScrollTrigger.refresh()
}

const observer = new MutationObserver((changes) => {
  const hasNewUi = changes.some((change) => Array.from(change.addedNodes).some((node) => node.nodeType === 1 && !node.classList?.contains('w2-gsap-depth')))
  if (hasNewUi) requestAnimationFrame(refreshAnimations)
})

observer.observe(document.documentElement, { childList: true, subtree: true })
requestAnimationFrame(refreshAnimations)
