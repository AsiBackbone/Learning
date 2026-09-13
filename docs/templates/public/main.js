// Keep DocFX search available while avoiding a multi-megabyte index download on
// landing-page visits that never use it. DocFX calls start() before it creates
// the search worker, which lets this lightweight proxy delay only that worker.

export default {
  start: function () {
    var deferSearch = document.querySelector('meta[name="docfx:defersearch"][content="true"]')
    var searchInput = document.getElementById('search-query')

    if (!deferSearch || !searchInput || !window.Worker) {
      return
    }

    var NativeWorker = window.Worker
    document.documentElement.dataset.searchIndex = 'deferred'
    searchInput.form.addEventListener('submit', function (event) {
      event.preventDefault()
    })

    function DeferredWorker(url, options) {
      var workerUrl = String(url)

      if (!workerUrl.endsWith('/search-worker.min.js') && workerUrl !== 'public/search-worker.min.js') {
        return new NativeWorker(url, options)
      }

      var worker = null
      var messages = []
      var proxy = this

      function activate() {
        if (worker) {
          return
        }

        document.documentElement.dataset.searchIndex = 'loading'
        worker = new NativeWorker(url, options)
        worker.onerror = function (event) {
          if (proxy.onerror) {
            proxy.onerror(event)
          }
        }
        worker.onmessageerror = function (event) {
          if (proxy.onmessageerror) {
            proxy.onmessageerror(event)
          }
        }
        worker.onmessage = function (event) {
          if (proxy.onmessage) {
            proxy.onmessage(event)
          }

          if (event.data && event.data.e === 'index-ready') {
            document.documentElement.dataset.searchIndex = 'ready'
          }

          if (event.data && event.data.e === 'index-ready' && searchInput.value) {
            searchInput.dispatchEvent(new Event('input', { bubbles: true }))
          }
        }

        messages.forEach(function (message) {
          if (message.transfer === undefined) {
            worker.postMessage(message.data)
          } else {
            worker.postMessage(message.data, message.transfer)
          }
        })
        messages = []
      }

      this.postMessage = function (data, transfer) {
        if (worker) {
          if (transfer === undefined) {
            worker.postMessage(data)
          } else {
            worker.postMessage(data, transfer)
          }
          return
        }

        messages.push({ data: data, transfer: transfer })
      }

      this.terminate = function () {
        messages = []
        if (worker) {
          worker.terminate()
        }
      }

      this.addEventListener = function (type, listener, eventOptions) {
        activate()
        worker.addEventListener(type, listener, eventOptions)
      }

      this.removeEventListener = function (type, listener, eventOptions) {
        if (worker) {
          worker.removeEventListener(type, listener, eventOptions)
        }
      }

      searchInput.addEventListener('focus', activate, { once: true })
      searchInput.addEventListener('input', activate, { once: true })
    }

    window.Worker = DeferredWorker
    searchInput.disabled = false
  }
}
