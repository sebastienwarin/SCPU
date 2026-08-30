const rootUri = "/"; //"http://slink.local/";

var app = angular.module('slink', []);
app.filter('toDate', function() {
    return function(input) {
        var dt = new Date(input * 1000);
        return dt.toLocaleDateString() + ' ' + dt.toLocaleTimeString();
    }
});
app.directive('focusMe', ['$timeout', function($timeout) {
  return {
    scope: { trigger: '=focusMe' },
    link: function(scope, element) {
      scope.$watch('trigger', function(value) {
        if (value === true) {
          $timeout(() => {
            element[0].focus();
            element[0].select();
          });
        }
      });
    }
  };
}]);
app.directive('scopeBinding', function () {
  return {
    link: function ($scope, element, attr) {
      $scope[attr.scopeBinding] = element[0];
    }
  }
});
app.filter('numberToHex', function() {
  return function(input) {
    var value = Number(input);
    if (isNaN(value)) return '----';
    return ('0000' + ((value & 0xFFFF).toString(16).toUpperCase())).slice(-4);
  };
});
app.directive('uploadImage', function() {
  return {
    link: function(scope, element) {
      var uploadSelectedFile = function() {
        var input = element[0];
        if (!input.files.length || !input.form) {
          return;
        }
        var file = input.files[0];
        if (file.name.length > 25) {
          scope.$apply(function() {
            scope.showError('Filename is too long. Use a name of 25 characters or fewer, including the extension.');
          });
          input.value = '';
          return;
        }
        var request = new XMLHttpRequest();
        var formData = new FormData(input.form);

        scope.$apply(function() {
          scope.imageUpload = { active: true, filename: file.name, progress: 0 };
        });

        request.upload.addEventListener('progress', function(event) {
          if (event.lengthComputable) {
            scope.$apply(function() {
              scope.imageUpload.progress = Math.round(100 * event.loaded / event.total);
            });
          }
        });
        request.addEventListener('load', function() {
          scope.$apply(function() {
            scope.imageUpload.active = false;
            if (request.status >= 200 && request.status < 400) {
              scope.imageUpload.progress = 100;
              scope.fetchROMS();
            } else {
              scope.showError(request.responseText || 'Image upload failed.');
            }
          });
          input.value = '';
        });
        request.addEventListener('error', function() {
          scope.$apply(function() {
            scope.imageUpload.active = false;
            scope.showError('Image upload failed. Check the connection to S-Link.');
          });
          input.value = '';
        });
        request.open('POST', input.form.action, true);
        request.send(formData);
      };
      element.on('change', uploadSelectedFile);
      scope.$on('$destroy', function() {
        element.off('change', uploadSelectedFile);
      });
    }
  };
});
app.directive('hexSelection', ['$document', function($document) {
  return {
    link: function(scope, element) {
      var finishSelection = function() {
        scope.$applyAsync(function() {
          scope.finishHexSelection();
        });
      };
      var focusViewer = function() {
        element[0].focus();
      };
      var copySelection = function(event) {
        var selectedHex = scope.getSelectedHex();
        var clipboard = event.clipboardData || (event.originalEvent && event.originalEvent.clipboardData);
        if (selectedHex && clipboard) {
          clipboard.setData('text/plain', selectedHex);
          event.preventDefault();
        }
      };
      $document.on('mouseup', finishSelection);
      element.on('mousedown', focusViewer);
      element.on('copy', copySelection);
      scope.$on('$destroy', function() {
        $document.off('mouseup', finishSelection);
        element.off('mousedown', focusViewer);
        element.off('copy', copySelection);
      });
    }
  };
}]);
app.controller('MainController', function($scope, $http, $interval, $sce) {

  $scope.flashOptions = {
    autoRunAfterFlash: true
  };

  $scope.clock_sources = [
    {'value': 0, 'label': 'NE555'},
    {'value': 1, 'label': 'S-Link'}
  ];
  $scope.clock_frequencies = [
    {'value': 5, 'label': '5 Hz'},
    {'value': 10, 'label': '10 Hz'},
    {'value': 100, 'label': '100 Hz'},
    {'value': 1000, 'label': '1 kHz'},
    {'value': 10000, 'label': '10 kHz'},
    {'value': 100000, 'label': '100 kHz'},
    {'value': 1000000, 'label': '1 MHz'},
    {'value': 2000000, 'label': '2 MHz'},
    {'value': 3000000, 'label': '3 MHz'},
    {'value': 4000000, 'label': '4 MHz'},
    {'value': 4500000, 'label': '4.5 MHz'},
    {'value': 5000000, 'label': '5 MHz'}
  ];
  $scope.romSearch = '';
  $scope.lastFlashedRom = '';
  $scope.memoryViewer = {
    target: 'RAM',
    address: 0,
    count: 256,
    view: 'inspector',
    addressMode: 'cpu',
    regions: [
      { name: 'Stack', className: 'stack', start: 0, end: 255 },
      { name: 'User page', className: 'user', start: 256, end: 1791 },
      { name: 'Reserved', className: 'reserved', start: 1792, end: 2047 }
    ],
    raw: null,
    snapshot: null,
    inspector: null,
    hexRows: [],
    selection: null,
    selecting: false,
    output: '',
    status: 'Idle',
    busy: false
  };
  $scope.imageUpload = {
    active: false,
    filename: '',
    progress: 0
  };

  $scope.formatBytes = function(value) {
    if (value === undefined || value === null || isNaN(value)) {
      return '-';
    }
    if (value < 1024) {
      return value + ' B';
    }
    if (value < 1024 * 1024) {
      return (value / 1024).toFixed(1) + ' KB';
    }
    return (value / (1024 * 1024)).toFixed(1) + ' MB';
  };

  $scope.formatFrequency = function(value) {
    if (value === undefined || value === null || isNaN(value)) {
      return '-';
    }
    if (value >= 1000000 && value % 1000000 === 0) {
      return (value / 1000000) + ' MHz';
    }
    if (value >= 1000000) {
      return (value / 1000000).toFixed(1) + ' MHz';
    }
    if (value >= 1000 && value % 1000 === 0) {
      return (value / 1000) + ' kHz';
    }
    if (value >= 1000) {
      return (value / 1000).toFixed(1) + ' kHz';
    }
    return value + ' Hz';
  };

  $scope.formatDuration = function(milliseconds) {
    if (milliseconds === undefined || milliseconds === null || isNaN(milliseconds)) {
      return '—';
    }
    if (milliseconds < 1000) {
      return milliseconds + ' ms';
    }
    return (milliseconds / 1000).toFixed(1) + ' s';
  };

  $scope.formatTransferSpeed = function(bytesPerSecond) {
    if (bytesPerSecond === undefined || bytesPerSecond === null || isNaN(bytesPerSecond)) {
      return '—';
    }
    if (bytesPerSecond < 1024) {
      return Math.round(bytesPerSecond) + ' B/s';
    }
    return $scope.formatBytes(bytesPerSecond) + '/s';
  };

  $scope.formatDateTime = function(value) {
    if (!value) {
      return '-';
    }
    var dt = new Date(value * 1000);
    return dt.toLocaleDateString() + ' ' + dt.toLocaleTimeString();
  };

  function toHex(value, width) {
    var hex = (Number(value) >>> 0).toString(16).toUpperCase();
    return new Array(Math.max(0, width - hex.length) + 1).join('0') + hex;
  }

  function buildHexRows(payload) {
    if (!payload || payload.data === undefined || payload.data === null) {
      return [];
    }

    var words = Array.isArray(payload.data) ? payload.data : [payload.data];
    var baseWordAddress = Number(payload.address) || 0;
    var rows = [];

    for (var offset = 0; offset < words.length; offset += 8) {
      var row = {
        byteAddress: toHex((baseWordAddress + offset) * 2, 8),
        words: []
      };

      for (var column = 0; column < 8; column++) {
        var index = offset + column;
        if (index < words.length) {
          var word = Number(words[index]) & 0xFFFF;
          var highByte = (word >> 8) & 0xFF;
          var lowByte = word & 0xFF;
          row.words.push({
            index: index,
            hex: toHex(word, 4),
            ascii: (highByte >= 32 && highByte <= 126 ? String.fromCharCode(highByte) : '.') +
              (lowByte >= 32 && lowByte <= 126 ? String.fromCharCode(lowByte) : '.')
          });
        }
      }
      rows.push(row);
    }

    return rows;
  }

  function ramPointerOffset(value) {
    value = Number(value) & 0xFFFF;
    return value >= 0x2000 && value <= 0x27FF ? value - 0x2000 : null;
  }

  function decodeRamSnapshot(payload) {
    if (!payload || !Array.isArray(payload.data) || payload.data.length < 2048) {
      return null;
    }

    var words = payload.data.map(function(value) { return Number(value) & 0xFFFF; });
    var definitions = [];
    for (var index = 0; index < 10; index++) {
      definitions.push({ name: 'R' + index, offset: 0x700 + index });
    }
    definitions = definitions.concat([
      { name: 'PARAM', label: 'Parameter register', offset: 0x70A },
      { name: 'RET', label: 'Return address', offset: 0x70B },
      { name: 'PEEK', label: 'Peek register', offset: 0x70C },
      { name: 'FP', label: 'Frame pointer', offset: 0x70E, pointer: true },
      { name: 'SP', label: 'Stack pointer', offset: 0x70F, pointer: true }
    ]);

    var registers = definitions.map(function(definition) {
      var value = words[definition.offset];
      return {
        name: definition.name,
        label: definition.label || definition.name,
        offset: definition.offset,
        value: value,
        hex: '0x' + toHex(value, 4),
        pointerOffset: definition.pointer ? ramPointerOffset(value) : null
      };
    });

    var spValue = words[0x70F];
    var fpValue = words[0x70E];
    var spOffset = ramPointerOffset(spValue);
    var fpOffset = ramPointerOffset(fpValue);
    var stackEntries = [];
    if (spOffset !== null && spOffset < 0x100) {
      for (var address = spOffset + 1; address <= 0xFF; address++) {
        var role = '';
        if (address === fpOffset) role = 'Frame pointer · previous FP';
        else if (fpOffset !== null && address === fpOffset + 1) role = 'Return address';
        stackEntries.push({
          offset: address,
          cpuAddress: 0x2000 + address,
          virtualAddress: 0x12000 + address,
          value: words[address],
          role: role
        });
      }
    }

    var frames = [];
    var visited = {};
    var currentFp = fpOffset;
    while (currentFp !== null && currentFp < 0x100 && !visited[currentFp] && frames.length < 32) {
      visited[currentFp] = true;
      frames.push({
        index: frames.length,
        fpOffset: currentFp,
        fpValue: 0x2000 + currentFp,
        returnAddress: currentFp < 0xFF ? words[currentFp + 1] : null
      });
      currentFp = ramPointerOffset(words[currentFp]);
    }

    return {
      capturedAt: new Date(),
      registers: registers,
      spOffset: spOffset,
      fpOffset: fpOffset,
      stackEntries: stackEntries,
      frames: frames,
      stackValid: spOffset !== null && spOffset < 0x100,
      fpValid: fpValue === 0 || (fpOffset !== null && fpOffset < 0x100)
    };
  }

  $scope.renderMemoryOutput = function() {
    if (!$scope.memoryViewer.raw) {
      $scope.memoryViewer.output = '';
      $scope.memoryViewer.hexRows = [];
      return;
    }
    $scope.memoryViewer.hexRows = buildHexRows($scope.memoryViewer.raw);
    $scope.memoryViewer.output = JSON.stringify($scope.memoryViewer.raw, null, 2);
    $scope.memoryViewer.selection = null;
  };

  $scope.setMemoryView = function(view) {
    $scope.memoryViewer.view = view;
    $scope.renderMemoryOutput();
  };

  $scope.captureRam = function() {
    $scope.memoryViewer.busy = true;
    $scope.memoryViewer.status = 'Capturing';
    $http.get(rootUri + 'ram/dump').then(function(response) {
      $scope.memoryViewer.snapshot = response.data;
      $scope.memoryViewer.inspector = decodeRamSnapshot(response.data);
      $scope.memoryViewer.raw = response.data;
      $scope.renderMemoryOutput();
      $scope.memoryViewer.view = 'inspector';
      $scope.memoryViewer.status = 'Ready';
    }).catch(function(error) {
      $scope.memoryViewer.status = 'Error';
      $scope.showError(error.data || 'RAM capture failed');
    }).finally(function() {
      $scope.memoryViewer.busy = false;
    });
  };

  $scope.formatRamAddress = function(offset, kind) {
    if (offset === null || offset === undefined) return '—';
    kind = kind || $scope.memoryViewer.addressMode;
    if (kind === 'virtual') return '0x' + toHex(0x12000 + offset, 5);
    if (kind === 'cpu') return '0x' + toHex(0x2000 + offset, 4);
    return '0x' + toHex(offset, 3);
  };

  $scope.getRamAddressModeLabel = function() {
    if ($scope.memoryViewer.addressMode === 'virtual') return 'S-CPU virtual';
    if ($scope.memoryViewer.addressMode === 'offset') return 'RAM offset';
    return 'CPU';
  };

  $scope.openRamOffset = function(offset) {
    if (offset === null || offset === undefined) return;
    $scope.memoryViewer.target = 'RAM';
    $scope.memoryViewer.address = Math.max(0, Math.min(0x7FF, offset));
    $scope.memoryViewer.count = Math.min(256, 0x800 - $scope.memoryViewer.address);
    $scope.memoryViewer.view = 'hex';
    $scope.readMemory();
  };

  $scope.setMemoryTarget = function(target) {
    if ($scope.memoryViewer.target === target) {
      return;
    }
    $scope.memoryViewer.target = target;
    $scope.memoryViewer.raw = null;
    $scope.memoryViewer.hexRows = [];
    $scope.memoryViewer.selection = null;
    $scope.memoryViewer.output = '';
    $scope.memoryViewer.status = 'Idle';
    $scope.memoryViewer.view = target === 'RAM' ? 'inspector' : 'hex';
    if (target === 'ROM') {
      $scope.memoryViewer.count = Math.min($scope.memoryViewer.count, 2048);
    }
  };

  $scope.getClockSourceLabel = function() {
    if (!$scope.status || !$scope.status.clock) {
      return '-';
    }
    for (var i = 0; i < $scope.clock_sources.length; i++) {
      if ($scope.clock_sources[i].value === $scope.status.clock.source) {
        return $scope.clock_sources[i].label;
      }
    }
    return '-';
  };

  $scope.getCurrentFrequencyLabel = function() {
    if (!$scope.status || !$scope.status.clock) {
      return '-';
    }
    if ($scope.status.progmode) {
      return 'Isolated';
    }
    if ($scope.status.clock.source != 1) {
      return 'External';
    }
    return $scope.formatFrequency($scope.status.clock.frequency);
  };

  $scope.getPowerLabel = function() {
    if (!$scope.status) {
      return '-';
    }
    return $scope.status.state ? 'ON' : 'OFF';
  };

  $scope.getClockStateLabel = function() {
    if (!$scope.status || !$scope.status.clock) {
      return '-';
    }
    return $scope.status.clock.auto ? 'Running' : 'Paused';
  };

  $scope.getProgrammerLabel = function() {
    if (!$scope.status) {
      return '-';
    }
    return $scope.status.progmode ? 'Enabled' : 'Idle';
  };

  $scope.isCurrentRom = function(file) {
    if (!file) {
      return false;
    }
    if ($scope.status && $scope.status.lastFlash && $scope.status.lastFlash.filename === file.path) {
      return true;
    }
    if ($scope.lastFlashedRom && $scope.lastFlashedRom === file.path) {
      return true;
    }
    return !!($scope.status && $scope.status.job && $scope.status.job.active && $scope.status.job.filename === file.path);
  };

  $scope.isActiveJobFile = function(file) {
    if (!file || !$scope.status || !$scope.status.job || !$scope.status.job.active) {
      return false;
    }
    var filePath = (file.path || '').replace(/^\/+/, '');
    var jobPath = ($scope.status.job.filename || '').replace(/^\/+/, '');
    return filePath === jobPath;
  };

  $scope.getJobProgress = function() {
    if (!$scope.status || !$scope.status.job || !$scope.status.job.totalSize) {
      return 0;
    }
    return Math.min(100, Math.max(0, 100 * $scope.status.job.written / $scope.status.job.totalSize));
  };

  $scope.getImageStatusLabel = function() {
    if ($scope.status && $scope.status.job && $scope.status.job.active) {
      var speed = $scope.status.job.speed ? ' (' + $scope.formatTransferSpeed($scope.status.job.speed) + ')' : '';
      return 'Writing ' + ($scope.status.job.filename || 'image') + ' · ' + $scope.getJobProgress().toFixed(0) + '%' + speed;
    }
    return ($scope.status && $scope.status.lastFlash && $scope.status.lastFlash.filename) ||
      $scope.lastFlashedRom || 'None';
  };

  $scope.getStorageUsagePercent = function() {
    if (!$scope.sysinfo || !$scope.sysinfo.SPIFFS || !$scope.sysinfo.SPIFFS.total) {
      return null;
    }
    return Math.round(100 * $scope.sysinfo.SPIFFS.used / $scope.sysinfo.SPIFFS.total);
  };

  $scope.isClockPresetActive = function(preset) {
    return !!($scope.status && $scope.status.clock && $scope.status.clock.frequency === preset.value);
  };

  $scope.setClockFrequencyPreset = function(preset) {
    if (!$scope.status || !$scope.status.clock) {
      return;
    }
    $scope.status.clock.frequency = preset.value;
    $scope.updateClock();
  };

  $scope.fetchROMS = function() {
    $http.get(rootUri + 'images')
      .then(function(response) {
        $scope.roms = response.data;
        if ($scope.roms && $scope.roms.files) {
          $scope.roms.files.sort(function(a, b) {
            return b.lastwrite - a.lastwrite;
          });
        }
      });
  };
  $scope.fetchStatus = function() {
    $http.get(rootUri + 'status')
      .then(function(response) {
        $scope.status = response.data;
      });
  };
  $scope.fetchSysinfo = function() {
    $http.get(rootUri + 'sysinfo')
      .then(function(response) {
        $scope.sysinfo = response.data;
      });
  };

  $scope.showError = function(text) {
    $scope.showModal("Error", text);
  }
  $scope.showModal = function(title, text) {
    $scope.dialog = {
      title: title,
      text: text,
      ok: () => $scope.dialogBox.close()
    };
    $scope.dialogBox.showModal();
  };

  $scope.enableEdit = function(file) {
    file.editing = true;
    file.newName = file.path;
  };
  $scope.cancelEdit = function(file) {
    file.editing = false;
  };
  $scope.handleRenameKey = function(event, file) {
    if (event.key === 'Enter') {
      $scope.renameFile(file);
    } else if (event.key === 'Escape') {
      $scope.cancelEdit(file);
    }
  };
  $scope.renameFile = function(file) {
    if (!file.newName || file.newName === file.path) {
      file.editing = false;
      return;
    }
    $http.post(rootUri + 'images/rename?file=' + encodeURIComponent(file.path) + '&newName=' + encodeURIComponent(file.newName))
    .then(function(response) {
      file.path = file.newName;
      file.editing = false;
    }).catch(function(error) {
      file.editing = false;
      $scope.showError(error.data);
    });
  };

  $scope.deleteRom = function(file) {
    $scope.dialog = {
      title: "Delete",
      text: 'Are you sure you want to delete image ' + file.path + '?',
      cancel: () => $scope.dialogBox.close(),
      confirm: () => {
        $scope.dialogBox.close();
        $http.delete(rootUri + 'images/file?name=' + encodeURIComponent(file.path))
          .then(function(response) {
            $scope.fetchROMS();
          })
          .catch(function(error) {
            $scope.showError(error.data || 'Unable to delete the image.');
          });
      }
    };
    $scope.dialogBox.showModal();
  };
  $scope.flashRom = function(file) {
    $scope.flashOptions.autoRunAfterFlash = true;
    $scope.dialog = {
      kind: 'flash',
      title: 'Confirm flash',
      file: file,
      cancel: () => $scope.dialogBox.close(),
      confirmLabel: 'Flash image',
      confirm: () => {
        $scope.dialogBox.close();
        var startFlash = function() {
          return $http.post(rootUri + 'images/flash?file=' + encodeURIComponent(file.path) + '&autoRun=' + ($scope.flashOptions.autoRunAfterFlash ? 'true' : 'false'));
        };
        var request = $scope.status.progmode ?
          startFlash() :
          $http.post(rootUri + 'programming/state?state=true').then(startFlash);
        request.catch(function(error) {
            $scope.showError(error.data);
          });
      }
    };
    $scope.dialogBox.showModal();
  };

  $scope.readMemory = function() {
    if (!$scope.memoryViewer) {
      return;
    }

    $scope.memoryViewer.busy = true;
    $scope.memoryViewer.status = 'Reading';

    var address = isNaN($scope.memoryViewer.address) ? 0 : Math.max(0, parseInt($scope.memoryViewer.address, 10));
    var requestedCount = isNaN($scope.memoryViewer.count) ? 1 : Math.max(1, parseInt($scope.memoryViewer.count, 10));
    requestedCount = Math.min(requestedCount, 2048);
    var capacity = $scope.memoryViewer.target === 'ROM' ? 0x10000 : 2048;
    address = Math.min(address, capacity - 1);
    var count = Math.min(requestedCount, capacity - address);
    $scope.memoryViewer.address = address;
    $scope.memoryViewer.count = requestedCount;

    var request;
    if ($scope.memoryViewer.target === 'ROM') {
      request = $http.get(rootUri + 'rom/read?address=' + address + '&count=' + count);
    } else {
      request = $http.get(rootUri + 'ram/read?address=' + address + '&count=' + count);
    }

    request.then(function(response) {
      $scope.memoryViewer.raw = response.data;
      $scope.renderMemoryOutput();
      $scope.memoryViewer.status = 'Ready';
    }).catch(function(error) {
      $scope.memoryViewer.raw = null;
      $scope.memoryViewer.output = '';
      $scope.memoryViewer.status = 'Error';
      $scope.showError(error.data || 'Memory read failed');
    }).finally(function() {
      $scope.memoryViewer.busy = false;
    });
  };

  $scope.readNextMemory = function() {
    if (!$scope.memoryViewer) {
      return;
    }
    var step = Math.max(1, parseInt($scope.memoryViewer.count, 10) || 1);
    if (isNaN($scope.memoryViewer.address)) {
      $scope.memoryViewer.address = 0;
    }
    var capacity = $scope.memoryViewer.target === 'ROM' ? 0x10000 : 2048;
    $scope.memoryViewer.address = Math.min(capacity - 1, $scope.memoryViewer.address + step);
    $scope.readMemory();
  };

  $scope.canReadNextMemory = function() {
    var address = parseInt($scope.memoryViewer.address, 10) || 0;
    var count = Math.max(1, parseInt($scope.memoryViewer.count, 10) || 1);
    var capacity = $scope.memoryViewer.target === 'ROM' ? 0x10000 : 2048;
    return address + count < capacity;
  };

  $scope.readPreviousMemory = function() {
    if (!$scope.memoryViewer) {
      return;
    }
    var step = Math.max(1, parseInt($scope.memoryViewer.count, 10) || 1);
    $scope.memoryViewer.address = Math.max(0, (parseInt($scope.memoryViewer.address, 10) || 0) - step);
    $scope.readMemory();
  };

  $scope.downloadMemory = function() {
    window.location.href = rootUri + ( $scope.memoryViewer.target === 'ROM' ? 'rom/dump.bin' : 'ram/dump.bin');
  };

  $scope.startHexSelection = function(index, event) {
    if (event && event.button !== 0) {
      return;
    }
    $scope.memoryViewer.selecting = true;
    $scope.memoryViewer.selection = { start: index, end: index };
    if (event) {
      event.preventDefault();
    }
  };

  $scope.extendHexSelection = function(index) {
    if ($scope.memoryViewer.selecting && $scope.memoryViewer.selection) {
      $scope.memoryViewer.selection.end = index;
    }
  };

  $scope.finishHexSelection = function() {
    $scope.memoryViewer.selecting = false;
  };

  $scope.isHexWordSelected = function(index) {
    var selection = $scope.memoryViewer.selection;
    return !!selection && index >= Math.min(selection.start, selection.end) && index <= Math.max(selection.start, selection.end);
  };

  $scope.getSelectedHex = function() {
    var selection = $scope.memoryViewer.selection;
    if (!selection || !$scope.memoryViewer.raw || !Array.isArray($scope.memoryViewer.raw.data)) {
      return '';
    }
    var first = Math.min(selection.start, selection.end);
    var last = Math.max(selection.start, selection.end);
    return $scope.memoryViewer.raw.data.slice(first, last + 1).map(function(word) {
      return toHex(Number(word) & 0xFFFF, 4);
    }).join(' ');
  };

  $scope.clearMemoryOutput = function() {
    if (!$scope.memoryViewer) {
      return;
    }
    $scope.memoryViewer.raw = null;
    $scope.memoryViewer.snapshot = null;
    $scope.memoryViewer.inspector = null;
    $scope.memoryViewer.hexRows = [];
    $scope.memoryViewer.selection = null;
    $scope.memoryViewer.output = '';
    $scope.memoryViewer.status = 'Idle';
  };

  $scope.setPowerState = function(state) {
    $scope.dialog = {
      title: "S-CPU",
      text: 'Are you sure you want to power ' + (state ? "ON" : "OFF") + ' the S-CPU?',
      cancel: () => $scope.dialogBox.close(),
      confirm: () => {
        $http.post(rootUri + 'control/power?state=' + state)
          .then(function(response) { }, function(error) {
            $scope.showError(error.data);
          });
        $scope.dialogBox.close();
      }
    };
    $scope.dialogBox.showModal();
  };

  $scope.setProgrammerMode = function(state) {
    $http.post(rootUri + 'programming/state?state=' + state)
      .then(function(response) { }, function(error) {
        $scope.showError(error.data);
      });
  };

  $scope.eraseFlash = function() {
    $scope.dialog = {
      title: "Erase Flash",
      text: 'Are you sure you want to erase the flash?',
      cancel: () => $scope.dialogBox.close(),
      confirm: () => {
        $http.post(rootUri + 'rom/erase')
          .then(function(response) { }, function(error) {
            $scope.showError(error.data);
          });
        $scope.dialogBox.close();
      }
    };
    $scope.dialogBox.showModal();
  };

  $scope.resetScpu = function() {
    $http.post(rootUri + 'control/reset')
      .then(function(response) { }, function(error) {
        $scope.showError(error.data);
      });
  };

  $scope.setClock = function(mode) {
    switch(mode) {
      case "start":
        $scope.updateClock(true);
        return;
      case "pause":
        $scope.updateClock(false);
        return;
      case "single":
      case "half":
        $http.post(rootUri + 'control/tick?full=false')
          .then(function(response) { }, function(error) {
            $scope.showError(error.data);
          });
        return;
      case "full":
        $http.post(rootUri + 'control/tick?full=true')
          .then(function(response) { }, function(error) {
            $scope.showError(error.data);
          });
        return;
    }
  };

  $scope.updateClock = function(autoTick) {
    if (autoTick === undefined) autoTick = $scope.status.clock.auto;
    $http.post(rootUri + 'control/clock?source=' + $scope.status.clock.source + '&frequency=' + $scope.status.clock.frequency + '&auto=' + autoTick)
      .then(function(response) { }, function(error) {
        $scope.showError(error.data);
      });
  };

  $scope.initUI = function() {
    $scope.fetchROMS();
    $scope.fetchStatus();
    $scope.fetchSysinfo();
  };

  $interval(function () {
      $scope.fetchSysinfo();
      if($scope.sse && $scope.sse.readyState == EventSource.CLOSED) {
        initSSE();
      }
  }, 5000);

  var initSSE = function() {
    if (!!window.EventSource) {
      var source = new EventSource(rootUri + 'events');
      source.addEventListener("open", function (e) {
          $scope.$apply(function() {
            $scope.initUI();
          });
        }, false);
      source.addEventListener("error", function (e) {
          if (e.target.readyState != EventSource.OPEN) {
          }
        }, false);
      source.addEventListener("Notify", function (e) {
          $scope.$apply(function () {
            $scope.showModal("S-Link", e.data);
          });
        }, false);
      source.addEventListener("StatusUpdate", function (e) {
          $scope.$apply(function () {
            $scope.currentJobStatus = e.data;
            if (e.data.indexOf("Erasing") >= 0) {
              $scope.currentJobPhase = "erasing";
            } else if (e.data.indexOf("Programming") >= 0) {
              $scope.currentJobPhase = "programming";
            } else if (e.data.indexOf("Verifying") >= 0) {
              $scope.currentJobPhase = "verifying";
            }
          });
        }, false);
      source.addEventListener("RomsUpdated", function (e) {
          $scope.$apply(function () {
            $scope.fetchROMS();
          });
        }, false);
      source.addEventListener("StateUpdated", function (e) {
          $scope.$apply(function () {
            $scope.fetchStatus();
          });
        }, false);
      source.addEventListener("JobReport", function (e) {
          $scope.$apply(function () {
            var report = JSON.parse(e.data);
            var jobFinish = $scope.status.job && $scope.status.job.active == true && report.active == false;
            $scope.status.job = report;
            if(jobFinish) {
                    if (report.flashResult === 1 && report.filename) {
                      $scope.lastFlashedRom = report.filename;
                    }
              var isSuccess = report.flashResult === 1;
              var modalIcon = isSuccess ? "✓" : "✗";
              var duration = (report.duration / 1000).toFixed(2);
              var speed = report.speed.toFixed(1);
              var speedKB = (speed/1024).toFixed(2);
              var sizeKB = (report.totalSize/1024).toFixed(2);
              var completionNoteHtml = '';
              if (!isSuccess) {
                completionNoteHtml = '<p style="margin-top: 12px;"><strong>Safety:</strong> Programming mode remains enabled so the S-CPU cannot resume against an incomplete or unverified ROM.</p>';
              } else if (report.autoRunRequested === true) {
                if (report.autoRunResult === 1) {
                  completionNoteHtml = '<p style="margin-top: 12px;"><strong>Note:</strong> Auto-run enabled: S-CPU reset and the configured clock source resumed.</p>';
                }
              } else {
                completionNoteHtml = '<p style="margin-top: 12px;"><strong>Note:</strong> Programming mode remains enabled for inspection or another operation.</p>';
              }

              var htmlContent = '<div class="modal-content">' +
                '<p style="font-size: 16px; margin-bottom: 16px;">' +
                (isSuccess ? 'The image has been flashed successfully!' : 'Flash operation failed!') +
                '</p>' +
                '<div class="flash-summary">' +
                '<div class="flash-summary-line"><span class="flash-summary-label">File:</span><span>' + report.filename + '</span></div>' +
                '<div class="flash-summary-line"><span class="flash-summary-label">Size:</span><span>' + report.totalSize + ' bytes (' + sizeKB + ' KB)</span></div>' +
                '<div class="flash-summary-line"><span class="flash-summary-label">Time:</span><span>' + duration + 's</span></div>' +
                '<div class="flash-summary-line"><span class="flash-summary-label">Speed:</span><span>' + speed + ' B/s (' + speedKB + ' KB/s)</span></div>' +
                '</div>' +
                completionNoteHtml +
                '</div>';

              $scope.dialog = {
                title: modalIcon + ' Flash image',
                html: $sce.trustAsHtml(htmlContent),
                ok: () => {
                  $scope.dialogBox.close();
                  $scope.currentJobStatus = "";
                  $scope.currentJobPhase = "";
                }
              };
              $scope.dialogBox.showModal();
              $scope.fetchStatus();
            }
          });
        }, false);
        $scope.sse = source;
    }
  };
  initSSE();
});
